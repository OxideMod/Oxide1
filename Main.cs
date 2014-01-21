using System;
using System.IO;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

using NLua;

using UnityEngine;

namespace Oxide
{
    /// <summary>
    /// The main class which the modified Rust binaries call into
    /// </summary>
    public class Main
    {
        private static Main singleton;

        #region Static Interface

        public static void Init()
        {
            singleton = new Main();
        }
        public static object Call(string name, object[] args)
        {

            return singleton.CallPlugin(name, args);
        }

        #endregion

        #region Utility

        public static void Log(string message)
        {
            Debug.Log(string.Format("[Oxide] {0}", message));
            File.AppendAllText(GetPath("oxidelog.txt"), message + Environment.NewLine);
        }
        public static void LogError(string message)
        {
            Debug.LogError(string.Format("[Oxide] {0}", message));
            File.AppendAllText(GetPath("oxidelog.txt"), string.Format("ERROR: {0}{1}", message, Environment.NewLine));
        }

        private static string serverpath;

        public static string GetPath(string filename)
        {
            if (Path.IsPathRooted(filename))
                return filename;
            else
                return Path.Combine(serverpath, filename);
        }

        #endregion

        private Lua lua;
        private Dictionary<string, LuaTable> plugins;

        private LuaFunction callunpacked;
        private LuaFunction createplugin;

        private Dictionary<string, Datafile> datafiles;

        private OxideComponent oxidecomponent;
        private GameObject oxideobject;

        private HashSet<Timer> timers;
        private HashSet<Timer> timers_toremove;
        private HashSet<AsyncWebRequest> webrequests;

        private Main()
        {
            try
            {
                // Determine the absolute path of the server instance
                serverpath = Path.GetDirectoryName(Path.GetFullPath(Application.dataPath));
                string[] cmdline = Environment.GetCommandLineArgs();
                for (int i = 0; i < cmdline.Length - 1; i++)
                {
                    string arg = cmdline[i].ToLower();
                    if (arg == "-serverinstancedir" || arg == "-oxidedir")
                    {
                        try
                        {
                            serverpath = Path.GetFullPath(cmdline[++i]);
                        }
                        catch (Exception ex)
                        {
                            LogError("Failed to read server instance directory from command line!");
                            LogError(ex.ToString());
                        }
                    }
                }

                // Ensure directories exist
                if (!Directory.Exists(serverpath)) Directory.CreateDirectory(serverpath);
                if (!Directory.Exists(GetPath("plugins"))) Directory.CreateDirectory(GetPath("plugins"));
                if (!Directory.Exists(GetPath("data"))) Directory.CreateDirectory(GetPath("data"));
                Log(string.Format("Loading at {0}...", serverpath));

                // Initialise the Unity component
                oxideobject = new GameObject("Oxide");
                oxidecomponent = oxideobject.AddComponent<OxideComponent>();
                oxidecomponent.Oxide = this;

                // Initialise needed maps and collections
                datafiles = new Dictionary<string, Datafile>();
                plugins = new Dictionary<string, LuaTable>();
                timers = new HashSet<Timer>();
                timers_toremove = new HashSet<Timer>();
                webrequests = new HashSet<AsyncWebRequest>();

                // Initialise the lua state
                lua = new Lua();
                lua["os"] = null;
                lua["io"] = null;
                lua["require"] = null;
                lua["dofile"] = null;
                lua["package"] = null;
                lua["luanet"] = null;

                // Register functions
                lua.NewTable("cs");
                RegisterFunction("cs.print", "lua_Print");
                RegisterFunction("cs.error", "lua_Error");
                RegisterFunction("cs.callplugins", "lua_CallPlugins");
                RegisterFunction("cs.findplugin", "lua_FindPlugin");
                RegisterFunction("cs.requeststatic", "lua_RequestStatic");
                RegisterFunction("cs.registerstaticmethod", "lua_RegisterStaticMethod");
                RegisterFunction("cs.requeststaticproperty", "lua_RequestStaticProperty");
                RegisterFunction("cs.requestproperty", "lua_RequestProperty");
                RegisterFunction("cs.requeststaticfield", "lua_RequestStaticField");
                RegisterFunction("cs.requestfield", "lua_RequestField");
                RegisterFunction("cs.requestenum", "lua_RequestEnum");
                RegisterFunction("cs.readproperty", "lua_ReadProperty");
                RegisterFunction("cs.readfield", "lua_ReadField");
                RegisterFunction("cs.castreadproperty", "lua_CastReadProperty");
                RegisterFunction("cs.castreadfield", "lua_CastReadField");
                RegisterFunction("cs.readulongpropertyasuint", "lua_ReadULongPropertyAsUInt");
                RegisterFunction("cs.readulongpropertyasstring", "lua_ReadULongPropertyAsString");
                RegisterFunction("cs.readulongfieldasuint", "lua_ReadULongFieldAsUInt");
                RegisterFunction("cs.readulongfieldasstring", "lua_ReadULongFieldAsString");
                RegisterFunction("cs.reloadplugin", "lua_ReloadPlugin");
                RegisterFunction("cs.getdatafile", "lua_GetDatafile");
                RegisterFunction("cs.dump", "lua_Dump");
                RegisterFunction("cs.createarrayfromtable", "lua_CreateArrayFromTable");
                RegisterFunction("cs.createtablefromarray", "lua_CreateTableFromArray");
                RegisterFunction("cs.gettype", "lua_GetType");
                RegisterFunction("cs.makegenerictype", "lua_MakeGenericType");
                RegisterFunction("cs.new", "lua_New");
                RegisterFunction("cs.newarray", "lua_NewArray");
                RegisterFunction("cs.convertandsetonarray", "lua_ConvertAndSetOnArray");
                RegisterFunction("cs.newtimer", "lua_NewTimer");
                RegisterFunction("cs.sendwebrequest", "lua_SendWebRequest");
                RegisterFunction("cs.throwexception", "lua_ThrowException");

                // Register constants
                lua.NewTable("bf");
                lua["bf.public_instance"] = BindingFlags.Public | BindingFlags.Instance;
                lua["bf.private_instance"] = BindingFlags.NonPublic | BindingFlags.Instance;
                lua["bf.public_static"] = BindingFlags.Public | BindingFlags.Static;
                lua["bf.private_static"] = BindingFlags.NonPublic | BindingFlags.Static;

                // Load the standard library
                Log("Loading standard library...");
                lua.LoadString(LuaOxideSTL.csfunc, "csfunc.stl").Call();
                lua.LoadString(LuaOxideSTL.json, "json.stl").Call();
                lua.LoadString(LuaOxideSTL.util, "util.stl").Call();
                lua.LoadString(LuaOxideSTL.type, "type.stl").Call();
                lua.LoadString(LuaOxideSTL.baseplugin, "baseplugin.stl").Call();
                lua.LoadString(LuaOxideSTL.rust, "rust.stl").Call();
                lua.LoadString(LuaOxideSTL.config, "config.stl").Call();
                lua.LoadString(LuaOxideSTL.plugins, "plugins.stl").Call();
                lua.LoadString(LuaOxideSTL.timer, "timer.stl").Call();
                lua.LoadString(LuaOxideSTL.webrequest, "webrequest.stl").Call();

                // Read back required functions
                callunpacked = lua["callunpacked"] as LuaFunction;
                createplugin = lua["createplugin"] as LuaFunction;

                // Load all plugins
                Log("Loading plugins...");
                string[] files = Directory.GetFiles(GetPath("plugins"), "*.lua");
                foreach (string file in files)
                {
                    string pluginname = Path.GetFileNameWithoutExtension(file);
                    LuaTable plugininstance = createplugin.Call()[0] as LuaTable;
                    lua["PLUGIN"] = plugininstance;
                    plugininstance["Filename"] = file;
                    plugininstance["Name"] = pluginname;
                    try
                    {
                        string code = File.ReadAllText(GetPath(file));
                        lua.LoadString(code, file).Call();
                        plugins.Add(pluginname, plugininstance);
                        lua["PLUGIN"] = null;
                    }
                    catch (NLua.Exceptions.LuaScriptException luaex)
                    {
                        LogError(string.Format("Failed to load plugin '{0}'! ({1})", file, luaex.Message));
                        LogError(luaex.StackTrace);
                    }
                }

                // Check plugin dependencies
                HashSet<string> toremove = new HashSet<string>();
                int numits = 0;
                while (numits == 0 || toremove.Count > 0)
                {
                    toremove.Clear();
                    foreach (var pair in plugins)
                    {
                        LuaTable dependencies = pair.Value["Depends"] as LuaTable;
                        if (dependencies != null)
                        {
                            foreach (var key in dependencies.Keys)
                            {
                                object value = dependencies[key];
                                if (value is string)
                                {
                                    if (!plugins.ContainsKey((string)value) || toremove.Contains((string)value))
                                    {
                                        LogError(string.Format("The plugin '{0}' depends on missing or unloaded plugin '{1}' and won't be loaded!", pair.Key, value));
                                        toremove.Add(pair.Key);
                                        break;
                                    }
                                }
                            }
                        }
                    }
                    foreach (string name in toremove)
                        plugins.Remove(name);
                    numits++;
                }

                // Call Init and PostInit on all plugins
                CallPlugin("Init", null);
                CallPlugin("PostInit", null);
            }
            catch (Exception ex)
            {
                LogError(string.Format("Error loading oxide! ({0})", ex));
                LogError(ex.StackTrace);
            }
        }

        public void Update()
        {
            foreach (Timer timer in timers)
                timer.Update();
            foreach (Timer timer in timers_toremove)
                timers.Remove(timer);
            timers_toremove.Clear();
            HashSet<AsyncWebRequest> toremove = new HashSet<AsyncWebRequest>();
            foreach (AsyncWebRequest req in webrequests)
            {
                req.Update();
                if (req.Complete) toremove.Add(req);
            }
            foreach (AsyncWebRequest req in toremove)
                webrequests.Remove(req);
        }

        private void RegisterFunction(string path, string name)
        {
            var method = GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
            if (method == null) throw new MissingMethodException(string.Format("Method by name {0} not found, couldn't register in Lua", name));
            lua.RegisterFunction(path, this, method);
        }

        #region Lua Functions

        private void lua_Print(string message)
        {
            Log(message);
        }
        private void lua_Error(string message)
        {
            LogError(message);
        }

        

        private object lua_CallPlugins(string methodname, LuaTable args, int argn)
        {
            object[] arr = new object[argn];
            for (int i = 0; i < argn; i++)
                arr[i] = args[i + 1];
            return CallPlugin(methodname, arr);
        }
        private LuaTable lua_FindPlugin(string name)
        {
            LuaTable result;
            if (!plugins.TryGetValue(name, out result)) result = null;
            return result;
        }

        private int lua_RequestStatic(string path, Type typ, string methodname)
        {
            MethodInfo[] allmethods = typ.GetMethods(BindingFlags.Static | BindingFlags.Public);
            HashSet<MethodInfo> candidates = new HashSet<MethodInfo>();
            foreach (MethodInfo method in allmethods)
            {
                if (method.Name == methodname)
                    candidates.Add(method);
            }

            if (candidates.Count == 0)
            {
                LogError(string.Format("Failed to locate static method {0} on type {1}!", methodname, typ));
                return 0;
            }
            else
                if (candidates.Count == 1)
                {
                    lua.RegisterFunction(path, null, candidates.Single());
                    return 1;
                }
                else
                {
                    // Lets filter out any methods that use generic, in or out params
                    var filtered = candidates.Where((x) =>
                    {
                        if (x.IsGenericMethod || x.ContainsGenericParameters) return false;
                        foreach (ParameterInfo pinfo in x.GetParameters())
                        {
                            if (!pinfo.IsRetval)
                            {
                                if (pinfo.IsOut) return false;
                                if (pinfo.IsIn) return false;
                            }
                        }
                        return true;
                    }).ToArray();
                    if (filtered.Length == 1)
                    {
                        lua.RegisterFunction(path, null, filtered[0]);
                        return 1;
                    }
                    else
                    {
                        //LogError(string.Format("Failed to locate suitable static method {0} on type {1}! (there were {2} candidates)", methodname, typ, filtered.Length));
                        /*for (int i = 0; i < filtered.Length; i++)
                            lua[path + "_Overload" + i.ToString()] = filtered[i];*/
                        lua[path] = filtered;
                        return filtered.Length;
                    }
                }
        }
        private void lua_RegisterStaticMethod(string path, MethodBase method)
        {
            lua.RegisterFunction(path, method);
        }
        private void lua_RequestStaticProperty(string path, Type typ, string propertyname)
        {
            var property = typ.GetProperty(propertyname, BindingFlags.Static | BindingFlags.Public);
            if (property == null)
            {
                LogError(string.Format("Failed to locate static property {0} on type {1}!", propertyname, typ));
                return;
            }
            lua[path] = property;
        }
        private void lua_RequestProperty(string path, Type typ, string propertyname)
        {
            var property = typ.GetProperty(propertyname, BindingFlags.Instance | BindingFlags.Public);
            if (property == null)
            {
                LogError(string.Format("Failed to locate instance property {0} on type {1}!", propertyname, typ));
                return;
            }
            lua[path] = property;
        }
        private void lua_RequestStaticField(string path, Type typ, string fieldname)
        {
            var field = typ.GetField(fieldname, BindingFlags.Static | BindingFlags.Public);
            if (field == null)
            {
                LogError(string.Format("Failed to locate static field {0} on type {1}!", fieldname, typ));
                return;
            }
            lua[path] = field;
        }
        private void lua_RequestField(string path, Type typ, string fieldname)
        {
            var field = typ.GetField(fieldname, BindingFlags.Instance | BindingFlags.Public);
            if (field == null)
            {
                LogError(string.Format("Failed to locate instance field {0} on type {1}!", fieldname, typ));
                return;
            }
            lua[path] = field;
        }
        private void lua_RequestEnum(string path, Type typ)
        {
            lua.NewTable(path);
            LuaTable dst = lua[path] as LuaTable;
            Array arr = Enum.GetValues(typ);
            for (int i = 0; i < arr.Length; i++)
            {
                object val = arr.GetValue(i);
                dst[val.ToString()] = val;
            }
        }

        private object lua_ReadProperty(PropertyInfo property, object obj = null)
        {
            return property.GetValue(obj, null);
        }
        private object lua_ReadField(FieldInfo field, object obj = null)
        {
            return field.GetValue(obj);
        }
        private object lua_CastReadProperty(PropertyInfo property, Type typ, object obj = null)
        {
            return Convert.ChangeType(property.GetValue(obj, null), typ);
        }
        private object lua_CastReadField(FieldInfo field, Type typ, object obj = null)
        {
            return Convert.ChangeType(field.GetValue(obj), typ);
        }
        private uint lua_ReadULongPropertyAsUInt(PropertyInfo property, object obj = null)
        {
            if (property == null) return 0;
            if (property.PropertyType != typeof(ulong))
            {
                LogError(string.Format("Failed to interpret ulong property {0} as int!", property.Name));
                return 0;
            }
            ulong value = (ulong)property.GetValue(obj, null);
            return (uint)(value & 0xFFFFFFFF);
        }
        private string lua_ReadULongPropertyAsString(PropertyInfo property, object obj = null)
        {
            if (property == null) return null;
            if (property.PropertyType != typeof(ulong))
            {
                LogError(string.Format("Failed to interpret ulong property {0} as string!", property.Name));
                return null;
            }
            ulong value = (ulong)property.GetValue(obj, null);
            return value.ToString();
        }
        private uint lua_ReadULongFieldAsUInt(FieldInfo field, object obj = null)
        {
            if (field == null) return 0;
            if (field.FieldType != typeof(ulong))
            {
                LogError(string.Format("Failed to interpret ulong field {0} as int!", field.Name));
                return 0;
            }
            ulong value = (ulong)field.GetValue(obj);
            return (uint)(value & 0xFFFFFFFF);
        }
        private string lua_ReadULongFieldAsString(FieldInfo field, object obj = null)
        {
            if (field == null) return null;
            if (field.FieldType != typeof(ulong))
            {
                LogError(string.Format("Failed to interpret ulong field {0} as string!", field.Name));
                return null;
            }
            ulong value = (ulong)field.GetValue(obj);
            return value.ToString();
        }

        private void lua_ReloadPlugin(string name)
        {
            ReloadPlugin(name);
        }

        private Datafile lua_GetDatafile(string name)
        {
            if (name.Contains('.')) return null;
            if (name.Contains('/')) return null;
            if (name.Contains('\\')) return null;
            Datafile result;
            if (!datafiles.TryGetValue(name, out result))
            {
                result = new Datafile(name);
                datafiles.Add(name, result);
            }
            else
            {
                result.Reload();
            }
            return result;
        }

        private void lua_Dump(GameObject obj)
        {
            Log(string.Format("Dumping all components of {0}...", obj));
            foreach (Component c in obj.GetComponents(typeof(Component)))
            {
                Log(c.ToString());
            }
        }

        private Array lua_CreateArrayFromTable(Type arrtype, LuaTable tbl, int argn)
        {
            Array arr = Activator.CreateInstance(arrtype.MakeArrayType(), new object[] { argn }) as Array;
            for (int i = 0; i < argn; i++)
                arr.SetValue(tbl[i + 1], i);
            return arr;
        }
        private LuaTable lua_CreateTableFromArray(Array arr)
        {
            lua.NewTable("_tmp");
            LuaTable result = lua["_tmp"] as LuaTable;
            lua["_tmp"] = null;
            for (int i = 0; i < arr.Length; i++)
                result[i + 1] = arr.GetValue(i);
            return result;
        }

        private void lua_ConvertAndSetOnArray(Array arr, int idx, object value, Type newtype)
        {
            value = Convert.ChangeType(value, newtype);
            arr.SetValue(value, idx);
        }

        private Type lua_GetType(string fullname)
        {
            bool blocked = false;
            if (fullname.Contains("Screen")) blocked = true;
            if (fullname.Contains("System") && !fullname.Contains("Assembly-CSharp"))
            {
                string[] spl = fullname.Split(',')[0].Split('.');
                if (spl.Length > 2)
                {
                    if (spl[1] != "Collections" && spl[1] != "Reflection") blocked = true;
                }
            }
            if (blocked)
            {
                Log(string.Format("Attempt to access blocked type '{0}'!", fullname));
                return null;
            }
            return Type.GetType(fullname);
        }
        private Type lua_MakeGenericType(Type typ, LuaTable args, int argn)
        {
            if (typ == null)
            {
                LogError(string.Format("Failed to make generic type (typ is null)!"));
                return null;
            }
            Type[] targs = new Type[argn];
            for (int i = 0; i < argn; i++)
            {
                object obj = args[i + 1];
                if (obj == null)
                {
                    LogError(string.Format("Failed to make generic type {0} (an arg is null)!", typ));
                    return null;
                }
                else if (obj is Type)
                    targs[i] = obj as Type;
                else
                {
                    LogError(string.Format("Failed to make generic type {0} (an arg is invalid)!", typ));
                    return null;
                }
            }
            try
            {
                return typ.MakeGenericType(targs);
            }
            catch (Exception ex)
            {
                LogError(string.Format("Failed to make generic type {0} (exception: {1})!", typ, ex));
                return null;
            }
        }
        private object lua_New(Type typ, LuaTable args, int argn)
        {
            if (typ == null)
            {
                LogError(string.Format("Failed to instantiate object (typ is null)!"));
                return null;
            }
            if (args == null)
            {
                try
                {
                    return Activator.CreateInstance(typ);
                }
                catch (Exception ex)
                {
                    LogError(string.Format("Failed to instantiate {0} (exception: {1})!", typ, ex));
                    return null;
                }
            }
            else
            {
                object[] oargs = new object[argn];
                for (int i = 0; i < argn; i++)
                    oargs[i] = args[i + 1];
                try
                {
                    return Activator.CreateInstance(typ, oargs);
                }
                catch (Exception ex)
                {
                    LogError(string.Format("Failed to instantiate {0} (exception: {1})!", typ, ex));
                    return null;
                }
            }
        }
        private object lua_NewArray(Type typ, int len)
        {
            return Activator.CreateInstance(typ.MakeArrayType(), len);
        }

        private Timer lua_NewTimer(float delay, int numiterations, LuaFunction func)
        {
            Action callback = new Action(() =>
            {
                try
                {
                    func.Call();
                }
                catch (Exception ex)
                {
                    LogError(string.Format("Error in timer: {0}", ex));
                }
            });
            Timer tmr = Timer.Create(delay, numiterations, callback);
            timers.Add(tmr);
            tmr.OnFinished += (t) => timers_toremove.Add(t);
            return tmr;
        }

        private bool lua_SendWebRequest(string url, LuaFunction func)
        {
            if (webrequests.Count > 3)
            {
                return false;
            }
            AsyncWebRequest req = new AsyncWebRequest(url);
            webrequests.Add(req);
            req.OnResponse += (r) =>
            {
                try
                {
                    func.Call(r.ResponseCode, r.Response);
                }
                catch (Exception ex)
                {
                    Debug.LogError(string.Format("Error in webrequest callback: {0}", ex));
                }
            };
            return true;
        }

        private void lua_ThrowException(string message)
        {
            throw new Exception(message);
        }

        #endregion

        private void ReloadPlugin(string name)
        {
            LuaTable plugin;
            if (!plugins.TryGetValue(name, out plugin)) return;
            CallSpecificPlugin(name, "Unload", null);
            plugins.Remove(name);
            string filename = (string)plugin["Filename"];
            LuaTable plugininstance = createplugin.Call()[0] as LuaTable;
            lua["PLUGIN"] = plugininstance;
            plugininstance["Filename"] = filename;
            plugininstance["Name"] = name;
            try
            {
                string code = File.ReadAllText(GetPath(filename));
                lua.LoadString(code, filename).Call();
                plugins.Add(name, plugininstance);
                lua["PLUGIN"] = null;
            }
            catch (NLua.Exceptions.LuaScriptException luaex)
            {
                LogError(string.Format("Failed to reload plugin '{0}'! ({1})", name, luaex.Message));
                LogError(luaex.StackTrace);
            }
            CallSpecificPlugin(name, "Init", null);
            CallSpecificPlugin(name, "PostInit", null);
        }

        private object CallPlugin(string name, object[] args)
        {
            /*if (args == null)
                Log(string.Format("Call to plugin method '{0}' with no args", name));
            else
                Log(string.Format("Call to plugin method '{0}' with {1} args", name, args.Length));*/
            /*if (args != null)
            {
                string[] tmp = new string[args.Length];
                for (int i = 0; i < args.Length; i++)
                {
                    if (args[i] == null)
                        tmp[i] = "null";
                    else
                        tmp[i] = args[i].ToString();
                }
                Log(string.Format("Call to plugin method '{0}' ({1})", name, string.Concat(tmp)));
            }*/

            lua.NewTable("_args");
            LuaTable argstable = lua["_args"] as LuaTable;
            if (args != null)
                for (int i = 0; i < args.Length; i++)
                    argstable[i + 2] = args[i];
            foreach (var pair in plugins)
            {
                LuaFunction func = pair.Value[name] as LuaFunction;
                if (func != null)
                {
                    argstable[1] = pair.Value;
                    try
                    {
                        object[] result = callunpacked.Call(func, argstable);
                        if (result != null && result.Length > 0)
                        {
                            //Log(string.Format("Returning result {0} from plugin call", result[0]));
                            return result[0];
                        }
                    }
                    catch (NLua.Exceptions.LuaScriptException luaex)
                    {
                        LogError(string.Format("Lua error ({0}:{2}): {1}", pair.Key, luaex.Message, luaex.Source));
                        LogError(luaex.StackTrace);
                        if (luaex.InnerException != null)
                            LogError(luaex.InnerException.ToString());
                    }
                    catch (Exception ex)
                    {
                        LogError(string.Format("Lua error ({0}): {1}", pair.Key, ex));
                        LogError(ex.StackTrace);
                        if (ex.InnerException != null)
                            LogError(ex.InnerException.ToString());
                    }
                }
            }
            return null;
        }
        private object CallSpecificPlugin(string pluginname, string name, object[] args)
        {
            LuaTable plugin;
            if (!plugins.TryGetValue(pluginname, out plugin)) return null;
            LuaFunction func = plugin[name] as LuaFunction;
            if (func != null)
            {
                lua.NewTable("_args");
                LuaTable argstable = lua["_args"] as LuaTable;
                if (args != null)
                    for (int i = 0; i < args.Length; i++)
                        argstable[i + 2] = args[i];
                argstable[1] = plugin;
                try
                {
                    object[] result = callunpacked.Call(func, argstable);
                    if (result != null && result.Length > 0) return result[0];
                }
                catch (Exception ex)
                {
                    LogError(string.Format("Lua error ({0}): {1}", pluginname, ex.Message));
                }
            }
            return null;
        }

    }
}
