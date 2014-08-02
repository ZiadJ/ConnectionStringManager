using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.Entity.Core.EntityClient;
using System.Data.SqlClient;
using System.IO;
using System.Web.Configuration;
using System.Xml.Linq;

namespace HCAppCode
{
    public class ConnectionSetting
    {
        public string Provider;
        public string ConnectionString;

        public ConnectionSetting(string provider)
        {
            Provider = provider;
        }

        public ConnectionSetting(string provider, string connectionString, params string[] connectionStringArguments)
        {
            ConnectionString = string.Format(connectionString, connectionStringArguments); ;
            Provider = provider;
        }

        public bool Test()
        {
            try
            {
                using (var conn = new SqlConnection(ConnectionString))
                {
                    conn.Open();
                    conn.Close();
                }
            }
            catch
            {
                return false;
            }

            return true;
        }
    }

    public class SqlConnectionSetting : ConnectionSetting
    {
        SqlConnectionStringBuilder Builder = new SqlConnectionStringBuilder();

        public SqlConnectionSetting(string server, string databaseNameOrPath, string username, string password, bool? multipleActiveResultSets, bool? integratedSecurity, bool? persistSecurityInfo, string applicationName)
            : base("System.Data.SqlClient")
        {

            if (databaseNameOrPath.Contains("\\") || databaseNameOrPath.ToLower().EndsWith(".mdf"))
                Builder.AttachDBFilename = databaseNameOrPath;
            else
                Builder.InitialCatalog = databaseNameOrPath;

            Builder.IntegratedSecurity = true;
            Builder.UserID = username;
            Builder.Password = password;
            if (multipleActiveResultSets.HasValue)
                Builder.MultipleActiveResultSets = multipleActiveResultSets.Value;

            if (integratedSecurity.HasValue)
                Builder.IntegratedSecurity = integratedSecurity.Value;

            if (persistSecurityInfo.HasValue)
                Builder.PersistSecurityInfo = persistSecurityInfo.Value;

            if (!string.IsNullOrEmpty(applicationName))
                Builder.ApplicationName = applicationName;

            var isLocal = string.IsNullOrEmpty(server) || server.Trim().ToLower() == "(local)";
            if (isLocal)
            {
                ConnectionString = "Server=(local);" + Builder.ToString();
            }
            else
            {
                Builder.DataSource = server;
                ConnectionString = Builder.ToString();
            }

            //return string.Format("Data Source={0};Initial Catalog={1};Integrated Security=False;User Id={2};Password={3};", server, database, username, password);
        }
    }

    public class EntityConnectionSetting : ConnectionSetting
    {
        EntityConnectionStringBuilder Builder = new EntityConnectionStringBuilder();

        public EntityConnectionSetting(string model, SqlConnectionSetting sqlConnString)
            : base("System.Data.EntityClient")
        {
            Builder.Metadata = string.Format("res://*/{0}.csdl|res://*/{0}.ssdl|res://*/{0}.msl", model);
            Builder.Provider = sqlConnString.Provider;
            Builder.ProviderConnectionString = sqlConnString.ConnectionString;

            ConnectionString = Builder.ToString();
        }

        public EntityConnectionSetting(string model, string modelProvider, string server, string database, string username, string password, bool? multipleActiveResultSets, bool? integratedSecurity, bool? persistSecurityInfo, string applicationName)
            : base("System.Data.EntityClient")
        {
            Builder.Metadata = string.Format("res://*/{0}.csdl|res://*/{0}.ssdl|res://*/{0}.msl", model);
            Builder.Provider = modelProvider;
            Builder.ProviderConnectionString = new SqlConnectionSetting(server, database, username, password, multipleActiveResultSets, integratedSecurity, persistSecurityInfo, applicationName).ConnectionString;
            ConnectionString = Builder.ToString();
        }

        public new bool Test()
        {
            try
            {
                using (var conn = new EntityConnection(ConnectionString))
                {
                    conn.Open();
                    conn.Close();
                }
            }
            catch
            {
                return false;
            }

            return true;
        }
    }

    public static class WebConfigConnectionStringManager
    {
        // Modification requires Admin rights may be via impersonation.
        public static Configuration Config = WebConfigurationManager.OpenWebConfiguration("~");

        public static T GetConfigurationSection<T>(string sectionName) where T : ConfigurationSection
        {
            try
            {
                return (T)Config.GetSection(sectionName) as T;
            }
            catch
            {
                return null;
            }
        }

        public static void SetConnectionStrings(Dictionary<string, ConnectionSetting> connectionStrings, bool encrypt)
        {
            var section = GetConfigurationSection<ConnectionStringsSection>("connectionStrings");
            if (section == null)
            {
                var doc = XDocument.Load(Config.FilePath);
                doc.Root.Element("connectionStrings").Remove();
                doc.Root.Add(new XElement("connectionStrings"));
                // Save to temp folder first then overwrite the original web.config file in case it doesn't have write access.
                doc.Save(Path.GetTempPath() + "Web.config");
                Utils.MoveFile(Path.GetTempPath() + "Web.config", Config.FilePath, false);
                section = GetConfigurationSection<ConnectionStringsSection>("connectionStrings");
            }

            var xmlConnections = new Dictionary<string, string>();
            foreach (ConnectionStringSettings con in section.ConnectionStrings)
                xmlConnections.Add(con.Name, con.ConnectionString);


            var isModified = false;

            foreach (var con in connectionStrings)
                if (!xmlConnections.ContainsKey(con.Key))
                {
                    section.ConnectionStrings.Add(new ConnectionStringSettings(con.Key, con.Value.ConnectionString.Replace("&quot;", "\""), con.Value.Provider));
                    isModified = true;
                }
            //else if (xmlConnections[con.Name] != con.ConnectionString)
            //{
            //    section.ConnectionStrings.Remove(con.Name);
            //    section.ConnectionStrings.Add(con);
            //    isModified = true;
            //}

            isModified = isModified || EncryptConnectionStrings(encrypt);

            if (isModified)
                Save(true);
        }


        public static void Save(bool deleteFileIfWriteAccessDenied)
        {
            var source = Path.GetTempPath() + "Web.config";
            var target = Config.FilePath;
            // Will only create file if changes exist.
            if (deleteFileIfWriteAccessDenied)
            {
                Config.SaveAs(source, ConfigurationSaveMode.Full, false);
                // Returns false if source file does not exist.
                Utils.MoveFile(source, target, false);
            }
            else
            {
                Config.Save();
            }
        }

        /// <summary>
        /// Encrypts connection strings(requires Admin rights may be via impersonation)
        /// </summary>
        /// <param name="encrypt"></param>
        public static bool EncryptConnectionStrings(bool encrypt)
        {
            var section = GetConfigurationSection<ConnectionStringsSection>("connectionStrings");
            // Toggle encryption.
            if (encrypt != section.SectionInformation.IsProtected)
            {
                if (encrypt)
                    section.SectionInformation.ProtectSection("DataProtectionConfigurationProvider"); // "RSAProtectedConfigurationProvider"
                else
                    section.SectionInformation.UnprotectSection();

                WebConfigConnectionStringManager.Save(true);

                if (encrypt != section.SectionInformation.IsProtected)
                {
                    throw new Exception("Configuration Encryption Failed");
                }

                return true;
            }

            return false;
        }
    }
}
