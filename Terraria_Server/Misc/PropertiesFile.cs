﻿using System.IO;
using System.Threading;
using System.Collections.Generic;
using System;

namespace Terraria_Server.Misc
{
    public class PropertiesFile
    {
        private const char EQUALS = '=';

        private Dictionary<String, String> propertiesMap;
        
        private String propertiesPath = String.Empty;
		
		public int Count
		{
			get { return propertiesMap.Count; }
		}
		
        public PropertiesFile(String propertiesPath)
        {
            propertiesMap = new Dictionary<String, String>();
            this.propertiesPath = propertiesPath;
        }

        public void Load() {
            //Verify that the properties file exists and we can create it if it doesn't.
            if (!File.Exists(propertiesPath))
            {
                File.WriteAllText(propertiesPath, String.Empty);
            }

            propertiesMap.Clear();
            StreamReader reader = new StreamReader(propertiesPath);
            try
            {
                String line;
                while ((line = reader.ReadLine()) != null)
                {
                    line = line.Trim();
                    int setterIndex = line.IndexOf(EQUALS);
                    if (setterIndex > 0 && setterIndex < line.Length)
                    {
                        propertiesMap.Add(line.Substring(0, setterIndex), line.Substring(setterIndex + 1));
                    }
                }
            }
            finally
            {
                reader.Close();
            }
        }

        public void Save()
        {
            var tmpName = propertiesPath + ".tmp" + (uint) (DateTime.UtcNow.Ticks % uint.MaxValue);
            var writer = new StreamWriter (tmpName);
            try
            {
                foreach (KeyValuePair<String, String> pair in propertiesMap)
                {
                    if (pair.Value != null)
                        writer.WriteLine(pair.Key + EQUALS + pair.Value);
                }
            }
            finally
            {
                writer.Close();
            }
            
            try
            {
                File.Replace (tmpName, propertiesPath, null, true);
                Program.tConsole.WriteLine ("Saved file \"{0}\".", propertiesPath);
            }
            catch (IOException e)
            {
                Program.tConsole.WriteLine ("Save to \"{0}\" failed: {1}", propertiesPath, e.Message);
            }
            catch (SystemException e)
            {
                Program.tConsole.WriteLine ("Save to \"{0}\" failed: {1}", propertiesPath, e.Message);
            }
            
        }

        public String getValue(String key)
        {
            if (propertiesMap.ContainsKey(key))
            {
                return propertiesMap[key];
            }
            return null;
        }

        public String getValue(String key, String defaultValue)
        {
            String value = getValue(key);
            if (value == null || value.Trim().Length < 0)
            {
                setValue(key, defaultValue);
                return defaultValue;
            }
            return value;
        }

        public int getValue(String key, int defaultValue)
        {
            int result;
            if (int.TryParse(getValue(key), out result))
            {
                return result;
            }

            setValue(key, defaultValue);
            return defaultValue;
        }

        public bool getValue(String key, bool defaultValue)
        {
            bool result;
            if (bool.TryParse(getValue(key), out result))
            {
                return result;
            }

            setValue(key, defaultValue);
            return defaultValue;
        }

        public void setValue(String key, String value)
        {
            propertiesMap[key] = value;
        }

        protected void setValue(String key, int value)
        {
            setValue(key, value.ToString());
        }

        protected void setValue(String key, bool value)
        {
            setValue(key, value.ToString());
        }
    }
}
