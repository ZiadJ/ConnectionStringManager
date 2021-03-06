ConnectionStringManager
=======================

Asp.net class to manage connection strings in web.config file. It will only add a connection string the web.config file if it's not already there. It can also encrypt the data so it can only be read by the machine on which it was created.

<b>Usage:</b>
```
var settings = new Dictionary<string, ConnectionSetting>();

// Create a plain SQL connection string.
settings.Add([ConnectionName], new SqlConnectionSetting(null, [DbName], [Username], [Password], true, false, true, null));

// Create an EF based connection string.
settings.Add([ConnectionName], new EntityConnectionSetting([ModelName], new SqlConnectionSetting(null, [DbName], [Username], [Password], true, false, true, "EntityFramework")));

// Write connection strings to the web.config file in encrypted form.
WebConfigConnectionStringManager.SetConnectionStrings(col, true);
```
