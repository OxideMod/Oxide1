using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

using NLua;
using NLua.Exceptions;

namespace Oxide
{
    /// <summary>
    /// Represents a plugin that modifies server behaviour in some way
    /// </summary>
    public class Plugin
    {
        public string Name { get; private set; }
        public string Title { get; private set; }
        public string Description { get; private set; }
        public string Author { get; private set; }
        public float Version { get; private set; }
        public string Filename { get; private set; }
        public string ShortFilename { get; private set; }
        public Lua LuaInstance { get; private set; }

        private LuaTable table;
        private Dictionary<string, LuaFunction> functionmap;
        private bool incall;

        /// <summary>
        /// Gets the currently active plugin (NOT THREADSAFE)
        /// </summary>
        public static Plugin CurrentPlugin { get; private set; }

        /// <summary>
        /// Returns the Lua table associated with this plugin
        /// </summary>
        public LuaTable Table
        {
            get
            {
                return table;
            }
        }

        public Plugin(Lua lua)
        {
            // Store the lua instance
            LuaInstance = lua;
        }

        /// <summary>
        /// Loads this plugin from file
        /// </summary>
        /// <param name="filename"></param>
        public bool Load(string filename)
        {
            // Store filename
            Filename = filename;
            ShortFilename = Path.GetFileName(filename);
            Name = Path.GetFileNameWithoutExtension(filename);

            // Check it exists
            if (!File.Exists(filename))
            {
                Logger.Error(string.Format("Failed to load plugin {0} (file not found)", Name));
                return false;
            }

            // Load it
            string script = File.ReadAllText(filename);

            // Attempt to compile
            LuaFunction func;
            try
            {
                func = LuaInstance.LoadString(script, ShortFilename);
            }
            catch (LuaScriptException ex)
            {
                Logger.Error(string.Format("Failed to load plugin {0}", Name), ex);
                return false;
            }

            // Create the plugin table
            LuaFunction createplugin = LuaInstance["createplugin"] as LuaFunction;
            if (createplugin == null) return false;
            table = createplugin.Call()[0] as LuaTable;
            table["Name"] = Name;
            table["Filename"] = filename;
            LuaInstance["PLUGIN"] = table;

            // Attempt to call
            try
            {
                func.Call();
            }
            catch (LuaScriptException ex)
            {
                Logger.Error(string.Format("Failed to load plugin {0}", Name), ex);
                return false;
            }
            LuaInstance["PLUGIN"] = null;

            // Get all functions
            functionmap = new Dictionary<string, LuaFunction>();
            foreach (var key in table.Keys)
            {
                if (key is string)
                {
                    var value = table[key];
                    if (value is LuaFunction)
                        functionmap.Add((string)key, value as LuaFunction);
                }
            }

            // Get base functions
            LuaTable metatable = (LuaInstance["getmetatable"] as LuaFunction).Call(table)[0] as LuaTable;
            if (metatable != null)
            {
                LuaTable basetable = metatable["__index"] as LuaTable;
                foreach (var key in basetable.Keys)
                {
                    if (key is string)
                    {
                        var value = basetable[key];
                        if (value is LuaFunction)
                            functionmap.Add((string)key, value as LuaFunction);
                    }
                }
            }

            // Check plugin descriptors
            if (!(table["Title"] is string))
            {
                Logger.Error(string.Format("Failed to load plugin {0} (invalid 'Title')", Name));
                return false;
            }
            if (!(table["Description"] is string))
            {
                Logger.Error(string.Format("Failed to load plugin {0} (invalid 'Description')", Name));
                return false;
            }
            if (!(table["Version"] is double))
            {
                Logger.Error(string.Format("Failed to load plugin {0} (invalid 'Version')", Name));
                return false;
            }
            if (!(table["Author"] is string))
            {
                Logger.Error(string.Format("Failed to load plugin {0} (invalid 'Author')", Name));
                return false;
            }

            // Load plugin descriptors
            Title = (string)table["Title"];
            Description = (string)table["Description"];
            Version = (float)(double)table["Version"];
            Author = (string)table["Author"];

            // Success
            return true;
        }

        /// <summary>
        /// Returns all hooks that this plugin implements
        /// </summary>
        /// <returns></returns>
        public IEnumerable<string> GetHooks()
        {
            return functionmap.Keys;
        }

        private static MethodBase LuaCallFunction;
        private static readonly Type[] LuaCallFunctionSig = new Type[] { typeof(object), typeof(object[]), typeof(Type[]) };
        private static readonly object[] LuaCallFunctionArgs = new object[3];

        private object CallFunction(LuaFunction func, object[] args)
        {
            // Check the method is loaded
            if (LuaCallFunction == null) LuaCallFunction = typeof(Lua).GetMethod("CallFunction", BindingFlags.NonPublic | BindingFlags.Instance, null, LuaCallFunctionSig, null);

            // Setup args
            LuaCallFunctionArgs[0] = func;
            LuaCallFunctionArgs[1] = args;
            LuaCallFunctionArgs[2] = null;
            
            // Call it
            return LuaCallFunction.Invoke(LuaInstance, LuaCallFunctionArgs);
        }

        /// <summary>
        /// Calls a hook on this plugin with a given set of arguments
        /// </summary>
        /// <param name="hookname"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        public object Call(string hookname, object[] args)
        {
            // Check incall
            //if (incall)
            //{
            //    Logger.Error(string.Format("Failed to call hook {0} on plugin {1} (coming from {2}) (recursive hook calls are forbidden)", hookname, Name, CurrentPlugin));
            //    return null;
            //}

            // Check that the function exists
            LuaFunction func;
            if (!functionmap.TryGetValue(hookname, out func)) return null;

            // Setup the args
            object[] luaargs;
            if (args != null)
            {
                luaargs = Main.Array(args.Length + 1);
                for (int i = 0; i < args.Length; i++)
                    luaargs[i + 1] = args[i];
            }
            else
                luaargs = Main.Array(1);
            luaargs[0] = table;


            // Make the call
            incall = true;
            Plugin oldcaller = CurrentPlugin;
            CurrentPlugin = this;
            object[] result;
            try
            {
                result = CallFunction(func, luaargs) as object[];
            }
            catch (LuaScriptException ex)
            {
                Logger.Error(string.Format("Failed to call hook {0} on plugin {1} (coming from {2})", hookname, Name, CurrentPlugin), ex);
                return null;
            }
            finally
            {
                CurrentPlugin = oldcaller;
                incall = false;
            }

            // Return the result
            if (result != null && result.Length > 0)
                return result[0];
            else
                return null;
        }

        public override string ToString()
        {
            return Name;
        }
    }
}
