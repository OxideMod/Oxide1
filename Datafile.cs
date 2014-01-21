using System;
using System.Collections.Generic;
using System.IO;

namespace Oxide
{
    /// <summary>
    /// Represents a data file than can store and recall plain text
    /// </summary>
    public class Datafile
    {
        private string text;
        private bool changed;
        private string filename;

        public Datafile(string name)
        {
            filename = Main.GetPath("data/" + name + ".txt");
            Reload();
        }

        /// <summary>
        /// Reloads this datafile
        /// </summary>
        public void Reload()
        {
            if (File.Exists(filename))
                text = File.ReadAllText(filename);
            else
                text = "";
        }

        /// <summary>
        /// Gets the plaintext stored in this datafile
        /// </summary>
        /// <returns></returns>
        public string GetText()
        {
            return text;
        }

        /// <summary>
        /// Sets the plaintext stored in this datafile
        /// </summary>
        /// <param name="txt"></param>
        public void SetText(string txt)
        {
            text = txt;
            changed = true;
        }

        /// <summary>
        /// Saves this datafile if changes have been made
        /// </summary>
        public void Save()
        {
            if (!changed) return;
            changed = false;
            File.WriteAllText(filename, text);
        }
    }
}
