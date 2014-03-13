using System;
using System.IO;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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
            return singleton.PluginManager.Call(name, args);
        }

        #endregion

        #region Utility

        private static object[][] arraypool = new object[16][];
        public static object[] Array(int size)
        {
            if (arraypool[size] == null) arraypool[size] = new object[size];
            return arraypool[size];
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

        private Dictionary<string, Datafile> datafiles;

        private OxideComponent oxidecomponent;
        private GameObject oxideobject;

        private HashSet<Timer> timers;
        private HashSet<AsyncWebRequest> webrequests;
        private Queue<AsyncWebRequest> webrequestQueue;

        private PluginManager pluginmanager;
        public PluginManager PluginManager { get { return pluginmanager; } }

        private Main()
        {
            try
            {
                // Load us
                Load();
            }
            catch (Exception ex)
            {
                Logger.Error(string.Format("Error loading oxide!"), ex);
            }
        }

        /// <summary>
        /// Loads Oxide
        /// </summary>
        private void Load()
        {
            // Initialise SSL
            System.Net.ServicePointManager.Expect100Continue = false;
            System.Net.ServicePointManager.ServerCertificateValidationCallback = (object sender, System.Security.Cryptography.X509Certificates.X509Certificate certificate, System.Security.Cryptography.X509Certificates.X509Chain chain,
                                       System.Net.Security.SslPolicyErrors sslPolicyErrors) => { return true; };
            System.Net.ServicePointManager.DefaultConnectionLimit = 200;

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
                        Logger.Error("Failed to read server instance directory from command line!", ex);
                    }
                }
            }

            // Ensure directories exist
            if (!Directory.Exists(serverpath)) Directory.CreateDirectory(serverpath);
            if (!Directory.Exists(GetPath("plugins"))) Directory.CreateDirectory(GetPath("plugins"));
            if (!Directory.Exists(GetPath("data"))) Directory.CreateDirectory(GetPath("data"));
            if (!Directory.Exists(GetPath("logs"))) Directory.CreateDirectory(GetPath("logs"));
            Logger.Message(string.Format("Loading at {0}...", serverpath));

            // Initialise the Unity component
            oxideobject = new GameObject("Oxide");
            oxidecomponent = oxideobject.AddComponent<OxideComponent>();
            oxidecomponent.Oxide = this;

            // Hook things that we can't hook using the IL injector
            var serverinit = UnityEngine.Object.FindObjectOfType(Type.GetType("ServerInit, Assembly-CSharp")) as MonoBehaviour;
            serverinit.gameObject.AddComponent<ServerInitHook>();

            // Initialise needed maps and collections
            datafiles = new Dictionary<string, Datafile>();
            timers = new HashSet<Timer>();
            webrequests = new HashSet<AsyncWebRequest>();
            webrequestQueue = new Queue<AsyncWebRequest>();

            // Initialise the lua state
            lua = new Lua();
            lua["os"] = null;
            lua["io"] = null;
            lua["require"] = null;
            lua["dofile"] = null;
            lua["package"] = null;
            lua["luanet"] = null;
            lua["load"] = null;

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
            RegisterFunction("cs.readpropertyandsetonarray", "lua_ReadPropertyAndSetOnArray");
            RegisterFunction("cs.readfieldandsetonarray", "lua_ReadFieldAndSetOnArray");
            RegisterFunction("cs.reloadplugin", "lua_ReloadPlugin");
            RegisterFunction("cs.getdatafile", "lua_GetDatafile");
            RegisterFunction("cs.getdatafilelist", "lua_GetDatafileList"); // LMP
            RegisterFunction("cs.removedatafile", "lua_RemoveDatafile"); // LMP
            RegisterFunction("cs.dump", "lua_Dump");
            RegisterFunction("cs.createarrayfromtable", "lua_CreateArrayFromTable");
            RegisterFunction("cs.createtablefromarray", "lua_CreateTableFromArray");
            RegisterFunction("cs.gettype", "lua_GetType");
            RegisterFunction("cs.makegenerictype", "lua_MakeGenericType");
            RegisterFunction("cs.new", "lua_New");
            RegisterFunction("cs.newarray", "lua_NewArray");
            RegisterFunction("cs.convertandsetonarray", "lua_ConvertAndSetOnArray");
            RegisterFunction("cs.getelementtype", "lua_GetElementType");
            RegisterFunction("cs.newtimer", "lua_NewTimer");
            RegisterFunction("cs.sendwebrequest", "lua_SendWebRequest");
            RegisterFunction("cs.postwebrequest", "lua_PostWebRequest");
            RegisterFunction("cs.throwexception", "lua_ThrowException");
            RegisterFunction("cs.gettimestamp", "lua_GetTimestamp");
            RegisterFunction("cs.loadstring", "lua_LoadString");
            RegisterFunction("cs.createperfcounter", "lua_CreatePerfCounter");

            // Register constants
            lua.NewTable("bf");
            lua["bf.public_instance"] = BindingFlags.Public | BindingFlags.Instance;
            lua["bf.private_instance"] = BindingFlags.NonPublic | BindingFlags.Instance;
            lua["bf.public_static"] = BindingFlags.Public | BindingFlags.Static;
            lua["bf.private_static"] = BindingFlags.NonPublic | BindingFlags.Static;

            // Load the standard library
            Logger.Message("Loading standard library...");
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
            lua.LoadString(LuaOxideSTL.validate, "validate.stl").Call();

            // Initialise the plugin manager
            pluginmanager = new PluginManager();

            // Iterate all physical plugins
            Logger.Message("Loading plugins...");
            string[] files = Directory.GetFiles(GetPath("plugins"), "*.lua");
            foreach (string file in files)
            {
                // Load and register the plugin
                Plugin p = new Plugin(lua);
                if (p.Load(file)) pluginmanager.AddPlugin(p);
            }

            // Call Init and PostInit on all plugins
            pluginmanager.Call("Init", null);
            pluginmanager.Call("PostInit", null);
        }

        /// <summary>
        /// Called by a Unity component, updates Oxide
        /// </summary>
        public void Update()
        {
            // Update timers
            foreach (Timer timer in timers.ToArray())
                timer.Update();

            // Update old web requests
            if (webrequests.Count < 3)
            {
                if (webrequestQueue.Count != 0 && webrequestQueue.Peek() != null)
                {
                    webrequests.Add(webrequestQueue.Dequeue());
                }
            }
            if (webrequests.Count == 0) return;
            foreach (AsyncWebRequest req in webrequests.ToArray())
            {
                req.Update();
                if (req.Complete) webrequests.Remove(req);
            }
        }

        /// <summary>
        /// Registers a .net function inside of Lua
        /// </summary>
        /// <param name="path"></param>
        /// <param name="name"></param>
        private void RegisterFunction(string path, string name)
        {
            var method = GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
            if (method == null) throw new MissingMethodException(string.Format("Method by name {0} not found, couldn't register in Lua", name));
            lua.RegisterFunction(path, this, method);
        }

        #region Lua Functions

        private void lua_Print(string message)
        {
            Logger.Message(message);
        }
        private void lua_Error(string message)
        {
            Logger.Error(string.Format("{0}: {1}", Plugin.CurrentPlugin, message));
        }

        private object lua_CallPlugins(string methodname, LuaTable args, int argn)
        {
            object[] arr = Array(argn);
            for (int i = 0; i < argn; i++)
                arr[i] = args[i + 1];
            return pluginmanager.Call(methodname, arr);
        }
        private LuaTable lua_FindPlugin(string name)
        {
            /*LuaTable result;
            if (!plugins.TryGetValue(name, out result)) result = null;
            return result;*/
            Plugin p = pluginmanager[name];
            if (p == null) return null;
            return p.Table;
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
                Logger.Error(string.Format("Failed to locate static method {0} on type {1}!", methodname, typ));
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
                Logger.Error(string.Format("Failed to locate static property {0} on type {1}!", propertyname, typ));
                return;
            }
            lua[path] = property;
        }
        private void lua_RequestProperty(string path, Type typ, string propertyname)
        {
            var property = typ.GetProperty(propertyname, BindingFlags.Instance | BindingFlags.Public);
            if (property == null)
            {
                Logger.Error(string.Format("Failed to locate instance property {0} on type {1}!", propertyname, typ));
                return;
            }
            lua[path] = property;
        }
        private void lua_RequestStaticField(string path, Type typ, string fieldname)
        {
            var field = typ.GetField(fieldname, BindingFlags.Static | BindingFlags.Public);
            if (field == null)
            {
                Logger.Error(string.Format("Failed to locate static field {0} on type {1}!", fieldname, typ));
                return;
            }
            lua[path] = field;
        }
        private void lua_RequestField(string path, Type typ, string fieldname)
        {
            var field = typ.GetField(fieldname, BindingFlags.Instance | BindingFlags.Public);
            if (field == null)
            {
                Logger.Error(string.Format("Failed to locate instance field {0} on type {1}!", fieldname, typ));
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
                Logger.Error(string.Format("Failed to interpret ulong property {0} as int!", property.Name));
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
                Logger.Error(string.Format("Failed to interpret ulong property {0} as string!", property.Name));
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
                Logger.Error(string.Format("Failed to interpret ulong field {0} as int!", field.Name));
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
                Logger.Error(string.Format("Failed to interpret ulong field {0} as string!", field.Name));
                return null;
            }
            ulong value = (ulong)field.GetValue(obj);
            return value.ToString();
        }

        private bool lua_ReloadPlugin(string name)
        {
            Plugin oldplugin = pluginmanager[name];
            if (oldplugin == null) return false;
            oldplugin.Call("Unload", null);
            pluginmanager.RemovePlugin(oldplugin);
            Plugin p = new Plugin(lua);
            if (!p.Load(oldplugin.Filename)) return false;
            pluginmanager.AddPlugin(p);
            p.Call("Init", null);
            p.Call("PostInit", null);
            p.Call("ServerStart", null);
            p.Call("OnDatablocksLoaded", null);
            p.Call("OnServerInitialized", null);
            return true;
        }
        private bool lua_LoadPlugin(string name)
        {
            Plugin oldplugin = pluginmanager[name];
            if (oldplugin != null) return false;
            Plugin p = new Plugin(lua);
            if (!p.Load(oldplugin.Filename)) return false;
            pluginmanager.AddPlugin(p);
            return true;
        }

        private LuaTable lua_GetDatafileList(string name)
        {
            if (name.Contains('.')) return null;
            if (name.Contains('/')) return null;
            if (name.Contains('\\')) return null;
            var result = Datafile.List(name);

            return lua_CreateTableFromArray(result);
        }

        private bool lua_RemoveDatafile(string name)
        {
            if (name.Contains('.')) return false;
            if (name.Contains('/')) return false;
            if (name.Contains('\\')) return false;
            if (Datafile.Remove(name))
            {
                datafiles.Remove(name);
                return true;
            }

            return false;
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
            Logger.Message(string.Format("Dumping all components of {0}...", obj));
            foreach (Component c in obj.GetComponents(typeof(Component)))
            {
                Logger.Message(c.ToString());
            }
        }

        private Array lua_CreateArrayFromTable(Type arrtype, LuaTable tbl, int argn)
        {
            object[] args = Array(1);
            args[0] = argn;
            Array arr = Activator.CreateInstance(arrtype.MakeArrayType(), args) as Array;
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
        private void lua_ReadFieldAndSetOnArray(Array arr, int idx, FieldInfo field, object target)
        {
            arr.SetValue(field.GetValue(target), idx);
        }
        private void lua_ReadPropertyAndSetOnArray(Array arr, int idx, PropertyInfo prop, object target)
        {
            arr.SetValue(prop.GetValue(target, null), idx);
        }

        private Type lua_GetType(string fullname)
        {
            bool blocked = false;
            if (fullname.Contains("Screen")) blocked = true;
            if (fullname.Contains("ServerFileSystem")) blocked = true;
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
                Logger.Error(string.Format("Attempt to access blocked type '{0}'!", fullname));
                return null;
            }
            return Type.GetType(fullname);
        }
        private Type lua_MakeGenericType(Type typ, LuaTable args, int argn)
        {
            if (typ == null)
            {
                Logger.Error(string.Format("Failed to make generic type (typ is null)!"));
                return null;
            }
            Type[] targs = new Type[argn];
            for (int i = 0; i < argn; i++)
            {
                object obj = args[i + 1];
                if (obj == null)
                {
                    Logger.Error(string.Format("Failed to make generic type {0} (an arg is null)!", typ));
                    return null;
                }
                else if (obj is Type)
                    targs[i] = obj as Type;
                else
                {
                    Logger.Error(string.Format("Failed to make generic type {0} (an arg is invalid)!", typ));
                    return null;
                }
            }
            try
            {
                return typ.MakeGenericType(targs);
            }
            catch (Exception ex)
            {
                Logger.Error(string.Format("Failed to make generic type {0}", typ), ex);
                return null;
            }
        }
        private object lua_New(Type typ, LuaTable args, int argn)
        {
            if (typ == null)
            {
                Logger.Error(string.Format("Failed to instantiate object (typ is null)!"));
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
                    Logger.Error(string.Format("Failed to instantiate {0} (exception: {1})!", typ, ex));
                    return null;
                }
            }
            else
            {
                object[] oargs = Array(argn);
                for (int i = 0; i < argn; i++)
                    oargs[i] = args[i + 1];
                try
                {
                    return Activator.CreateInstance(typ, oargs);
                }
                catch (Exception ex)
                {
                    Logger.Error(string.Format("Failed to instantiate {0} (exception: {1})!", typ, ex));
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
            Plugin callerplugin = Plugin.CurrentPlugin;
            Action callback = new Action(() =>
            {
                try
                {
                    func.Call();
                }
                catch (Exception ex)
                {
                    //Logger.Error(string.Format("Error in timer ({1}): {0}", ex));
                    Logger.Error(string.Format("Error in timer ({0})", callerplugin), ex);
                }
            });
            Timer tmr = Timer.Create(delay, numiterations, callback);
            timers.Add(tmr);
            tmr.OnFinished += (t) => timers.Remove(t);
            return tmr;
        }

        private bool lua_SendWebRequest(string url, LuaFunction func)
        {
            AsyncWebRequest req = new AsyncWebRequest(url);
            webrequestQueue.Enqueue(req);
            Plugin callerplugin = Plugin.CurrentPlugin;
            req.OnResponse += (r) =>
            {
                try
                {
                    func.Call(r.ResponseCode, r.Response);
                }
                catch (Exception ex)
                {
                    //Debug.LogError(string.Format("Error in webrequest callback: {0}", ex));
                    Logger.Error(string.Format("Error in webrequest callback ({0})", callerplugin), ex);
                }
            };
            return true;
        }

        private bool lua_PostWebRequest(string url, string postdata, LuaFunction func)
        {
            AsyncWebRequest req = new AsyncWebRequest(url, postdata);
            webrequestQueue.Enqueue(req);
            Plugin callerplugin = Plugin.CurrentPlugin;
            req.OnResponse += (r) =>
            {
                try
                {
                    func.Call(r.ResponseCode, r.Response);
                }
                catch (Exception ex)
                {
                    //Debug.LogError(string.Format("Error in webrequest callback: {0}", ex));
                    Logger.Error(string.Format("Error in webrequest callback ({0})", callerplugin), ex);
                }
            };
            return true;
        }

        private void lua_ThrowException(string message)
        {
            throw new Exception(message);
        }

        private Type lua_GetElementType(Array arr, int idx)
        {
            return arr.GetValue(idx).GetType();
        }

        private static readonly DateTime epoch = new DateTime(1970, 1, 1);
        private uint lua_GetTimestamp()
        {
            DateTime now = DateTime.UtcNow;
            return (uint)now.Subtract(epoch).TotalSeconds;
        }
        private LuaFunction lua_LoadString(string str, string name)
        {
            return lua.LoadString(str, name);
        }
        private System.Diagnostics.PerformanceCounter lua_CreatePerfCounter(string category, string counter, string instance, bool rdonly)
        {
            return new System.Diagnostics.PerformanceCounter(category, counter, instance, rdonly);
        }

        #endregion

    }
}
