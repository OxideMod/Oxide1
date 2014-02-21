using System;
using System.Collections.Generic;
using System.Linq;

namespace Oxide
{
    /// <summary>
    /// Keeps track and interfaces with all loaded plugins
    /// </summary>
    public class PluginManager
    {
        private HashSet<Plugin> allplugins;

        private Dictionary<string, HashSet<Plugin>> hooks;

        public PluginManager()
        {
            allplugins = new HashSet<Plugin>();
            hooks = new Dictionary<string, HashSet<Plugin>>();
        }

        /// <summary>
        /// Adds a plugin to this manager
        /// </summary>
        /// <param name="plugin"></param>
        public void AddPlugin(Plugin plugin)
        {
            // Add to all plugins
            allplugins.Add(plugin);

            // Register all hooks
            foreach (string hookname in plugin.GetHooks())
            {
                HashSet<Plugin> set;
                if (!hooks.TryGetValue(hookname, out set))
                {
                    set = new HashSet<Plugin>();
                    hooks.Add(hookname, set);
                }
                set.Add(plugin);
            }
        }

        /// <summary>
        /// Removes a plugin from this manager
        /// </summary>
        /// <param name="plugin"></param>
        public void RemovePlugin(Plugin plugin)
        {
            // Remove from all plugins
            allplugins.Remove(plugin);

            // Unregister any hooks
            foreach (var pair in hooks)
                pair.Value.Remove(plugin);
        }

        /// <summary>
        /// Calls a hook on all plugins
        /// </summary>
        /// <param name="hookname"></param>
        /// <param name="args"></param>
        public object Call(string hookname, object[] args)
        {
            // Locate the plugin set
            HashSet<Plugin> set;
            if (!hooks.TryGetValue(hookname, out set)) return null;

            // Loop each plugin
            foreach (Plugin plugin in set.ToArray())
            {
                // Make the call
                object result = plugin.Call(hookname, args);

                // Is there a return value?
                if (result != null) return result;
            }

            // No return value
            return null;
        }

        /// <summary>
        /// Returns an enumerable for all loaded plugins
        /// </summary>
        /// <returns></returns>
        public IEnumerable<Plugin> GetPlugins()
        {
            return allplugins;
        }

        /// <summary>
        /// Returns a specific plugin
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public Plugin this[string name]
        {
            get
            {
                // Try and find it
                foreach (Plugin plugin in allplugins)
                    if (plugin.Name == name)
                        return plugin;

                // Not found
                return null;
            }
        }

    }
}
