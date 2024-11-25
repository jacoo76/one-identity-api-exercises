// Import necessary namespaces
using System;
using System.IO;
using System.Web;
using System.Net;
using System.Threading;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Linq;
using System.Text;
using VI.Base;
using VI.DB;
using VI.DB.Entities;
using QBM.CompositionApi.ApiManager;
using QBM.CompositionApi.Definition;
using QBM.CompositionApi.Crud;
using QER.CompositionApi.Portal;
using QBM.CompositionApi.Handling;
using System.Collections;
using System.Security.Cryptography;
using VI.DB.DataAccess;
using VI.DB.Sync;


namespace QBM.CompositionApi
{
    // The MyExercises class implements the IApiProvider interfaces for the PortalApiProject
    public class MyExercises : IApiProviderFor<QER.CompositionApi.Portal.PortalApiProject>, IApiProvider
    {
        // The Build method is used to define API methods using the IApiBuilder
        public void Build(IApiBuilder builder)
        {
            // Add a GET method named "api_exercises/get_helloworld" to the API
            builder
                .AddMethod(Method.Define("api_exercises/get_helloworld")
                .HandleGet((qr) =>
                {
                    // Retrieve the UID of the currently logged-in user from the session
                    // var loggedinuser = qr.Session.User().Uid;

                    return new DataObject { Message = "Hello world" };
                }
                ));

            // Add a POST method on the endpoint "api_exercises/helloworld/post" of the API
            builder
                .AddMethod(Method.Define("api_exercises/helloworld/post")
                // The function creates a new DataObject with a Message property that includes the input from the posted object
                // PostedMessage: The type of the data expected in the request body - needs to be defined in the namespace
                // DataObject: The type of the data that will be returned as a JSON response - needs to be defined in the namespace
                // posted: An instance of PostedMessage containing the data from the request body
                .Handle<PostedMessage, DataObject>("POST", (posted, qr) => new DataObject
                {
                    Message = "Hello " + posted.Input
                }));



            //
            //
            // GENERAL FLOW: query - tryget - tryget(success) - else (return error message)
            //
            //




            // Ex 03 - GET all fields of an AAD Group (UIDGroup as parameter) - endpoint: "api_exercises/aad_requests/get_all_fields"
            // endpoint example /api_exercises/aad_requests/get_all_fields?UIDGroup=048daecd-4925-49b8-b2b3-32cefda548b8
            builder.AddMethod(Method.Define("api_exercises/aad_requests/get_all_fields")
                .WithParameter("UIDGroup", typeof(string))
                .HandleGet(async (qr, ct) =>
                {
                    var loggedinUser = qr.Session.User().Uid;
                    var UID_Agroup = qr.Parameters.Get<string>("UIDGroup");
                    // API_aad_ResetPasswords_UID is the hardcoded UID of an AAD Group (in this case aad_ResetPasswords) - defined on Designer -> Base data -> General -> Configuration Params
                    // string aeRoleid = await qr.Session.Config().GetConfigParmAsync("Custom\\API_aad_ResetPasswords_UID").ConfigureAwait(false);
                    // var aeRoleid = "048daecd-4925-49b8-b2b3-32cefda548b8";

                    var query = Query.From("AADGroup")
                        .Select("UID_AADGroup", "DisplayName", "Description", "IsForITShop", "MailNickName")
                        .Where(string.Format(@"UID_AADGroup = '{0}'", UID_Agroup));

                    var tryGet = await qr.Session.Source().TryGetAsync(query, EntityLoadType.DelayedLogic).ConfigureAwait(false);

                    if (tryGet.Success)
                    {
                        // Convert the retrieved entity to a ReturnedAADGroupDetails object and return it
                        return await ReturnedAADGroupDetails.fromEntity(tryGet.Result, qr.Session).ConfigureAwait(false);
                    }
                    else
                    {
                        return null;
                    }

                })

                );


            // Ex 03 - POST to create an AAD Group - endpoint: "api_exercises/aad_requests/create_aad_group"
            builder.AddMethod(Method.Define("api_exercises/aad_requests/create_aad_group")
                .Handle<PostedAADGroup>("POST", async (posted, qr, ct) =>
                {
                    // UID_Person of the current logged in user
                    var uidperson = qr.Session.User().Uid;


                    string compareStart = "aad";
                    try
                    {
                        if (!posted.display_name.StartsWith(compareStart, true, Thread.CurrentThread.CurrentCulture))
                        {
                            throw new System.Web.HttpException("An error occured. DisplayName of the Azure Active Directory Group should always has the prefix 'aad'.", 504);
                        }

                        // Variables initialization to store the data from the posted request
                        string display_name = posted.display_name;
                        string UIDOrganization_tenant = posted.UIDOrganization_tenant; // if not exists an error is thrown to inform the user - automa
                        string group_alias = posted.group_alias;
                        string group_description = posted.group_description;

                        // Create a new 'AADGroup' entity
                        var newAADGroup = await qr.Session.Source().CreateNewAsync("AADGroup",
                        new EntityParameters
                        {
                            CreationType = EntityCreationType.DelayedLogic
                        }, ct).ConfigureAwait(false);

                        // Set the values for the new 'AADGroup' entity
                        await newAADGroup.PutValueAsync("DisplayName", display_name, ct).ConfigureAwait(false);
                        await newAADGroup.PutValueAsync("UID_AADOrganization", UIDOrganization_tenant, ct).ConfigureAwait(false);
                        await newAADGroup.PutValueAsync("MailNickName", group_alias, ct).ConfigureAwait(false);
                        await newAADGroup.PutValueAsync("Description", group_description, ct).ConfigureAwait(false);

                        // Start Unit of Work to save the new entity to the database
                        using (var u = qr.Session.StartUnitOfWork())
                        {
                            await u.PutAsync(newAADGroup, ct).ConfigureAwait(false);  // Add the new entity to the unit of work
                            await u.CommitAsync(ct).ConfigureAwait(false);  // Commit the transaction to persist changes
                        }
                    }
                    catch (Exception)
                    {
                        throw;
                    }

                }));


            // Ex 03 - PUT to update an AAD Group - endpoint "api_exercises/aad_requests/update_aad_group"
            builder.AddMethod(Method.Define("api_exercises/aad_requests/update_aad_group")
                .Handle<PostedAADGroupToUpdate, string>("PUT", async (posted, qr, ct) =>
                {
                    string compareStart = "aad";
                    try
                    {
                        if (!posted.display_name.StartsWith(compareStart, true, Thread.CurrentThread.CurrentCulture))
                        {
                            throw new System.Web.HttpException("An error occurred. DisplayName of the Azure Active Directory Group should always have the prefix 'aad'. Please adjust and try again.", 504);
                        }

                        // Variables initialization to store the data from the posted request
                        string display_name = posted.display_name;
                        string UIDOrganization_tenant = posted.UIDOrganization_tenant; // if not exists an error is thrown to inform the user - automaticaly from the system
                        string group_alias = posted.group_alias;
                        string group_description = posted.group_description;

                        // Find the AAD Entity needs to be updated
                        // Build a query to select all columns from the "AADGroup" table where UID_AADGroup matches
                        var query1 = Query.From("AADGroup")
                                          .Select("*")
                                          .Where(string.Format("UID_AADGroup = '{0}'", posted.uid_aadGroup));

                        // Attempt to retrieve the entity asynchronously
                        var tryget = await qr.Session.Source()
                                           .TryGetAsync(query1, EntityLoadType.DelayedLogic, ct)
                                           .ConfigureAwait(false);
                        // Console.WriteLine(tryget.Result);

                        var queryTenants = Query.From("AADOrganization")
                                          .Select("*")
                                          .Where(string.Format("UID_AADOrganization = '{0}'", posted.UIDOrganization_tenant));

                        var tryGetTenant = await qr.Session.Source()
                                           .TryGetAsync(queryTenants, EntityLoadType.DelayedLogic, ct)
                                           .ConfigureAwait(false);


                        if (tryget.Success)
                        {
                            if (tryGetTenant.Success)
                            {
                                if (!String.IsNullOrEmpty(display_name))
                                {
                                    await tryget.Result.PutValueAsync("DisplayName", display_name, ct).ConfigureAwait(false);
                                }
                                if (!String.IsNullOrEmpty(UIDOrganization_tenant))
                                {
                                    await tryget.Result.PutValueAsync("UID_AADOrganization", UIDOrganization_tenant, ct).ConfigureAwait(false);
                                }
                                if (!String.IsNullOrEmpty(group_alias))
                                {
                                    await tryget.Result.PutValueAsync("MailNickName", group_alias, ct).ConfigureAwait(false);
                                }
                                if (!String.IsNullOrEmpty(group_description))
                                {
                                    await tryget.Result.PutValueAsync("Description", group_description, ct).ConfigureAwait(false);
                                }
                                // Start Unit of Work to save the new entity to the database
                                using (var u = qr.Session.StartUnitOfWork())
                                {
                                    await u.PutAsync(tryget.Result, ct).ConfigureAwait(false);  // Add the new entity to the unit of work
                                    await u.CommitAsync(ct).ConfigureAwait(false);  // Commit the transaction to persist changes
                                }
                                return $"Azure Active Directory Group updated. Display name: {display_name}";
                            }
                            else
                            {
                                return $"The UID of the Tenant ({UIDOrganization_tenant}) is not exist. Please choose a correct tenant to proceed.";
                            }
                        }
                        else
                        {
                            return "An error occured. A correct UID_AADGroup is mandatory to update an AAD Group. Failed to fetch the data from the request.";
                        }



                    }
                    catch (Exception)
                    {
                        throw;
                    }
                }));


            // Ex 03 - DELETE to delete an AAD Group - endpoint "api_exercises/aad_requests/delete_aad_group"
            builder.AddMethod(Method.Define("api_exercises/aad_requests/delete_aad_group")
                .Handle<PostedAADGroup_UID, ReturnedDetails>("DELETE", async (posted, qr, ct) =>
                {
                    string UID_AAD_Group = posted.uid_aad_group;

                    // Build a query to select all columns from the table where UID_AADGroup matches
                    var query1 = Query.From("AADGroup")
                                      .Select("*")
                                      .Where(string.Format("UID_AADGroup = '{0}'", posted.uid_aad_group));

                    var tryGetGroup = await qr.Session.Source()
                    .TryGetAsync(query1, EntityLoadType.DelayedLogic, ct)
                                           .ConfigureAwait(false);

                    if (tryGetGroup.Success)
                    {
                        // Start a unit of work for transactional database operations
                        using (var u = qr.Session.StartUnitOfWork())
                        {
                            // Get the entity to be deleted
                            var objecttodelete = tryGetGroup.Result;

                            // Mark the entity for deletion
                            objecttodelete.MarkForDeletion();

                            // Save the changes to the unit of work
                            await u.PutAsync(objecttodelete, ct).ConfigureAwait(false);

                            // Commit the unit of work to persist changes to the database
                            await u.CommitAsync(ct).ConfigureAwait(false);
                        }
                        // Return a successful response by converting the entity to ReturnedClass
                        return await ReturnedDetails.fromEntity(tryGetGroup.Result, qr.Session).ConfigureAwait(false);
                    }
                    else
                    {
                        // If the entity was not found, return an error with a custom message and error code
                        return await ReturnedDetails.Error(
                            string.Format("No AAD Groups found with AAD_UIDGroup '{0}'. Please try again providing a correct UID_AADGroup.", posted.uid_aad_group),
                            681
                        ).ConfigureAwait(false);
                    }

                }));



            // Ex 04 - GET all AAD Groups based on filtering - endpoint: "api_exercises/aad_requests/get_all_aad_groups" with parameters (parameters: "is_for_ITshop", "tenant_uid"
            builder.AddMethod(Method.Define("api_exercises/aad_requests/get_all_aad_groups")
                .WithParameter("is_for_ITshop", typeof(string))
                .WithParameter("tenant_uid", typeof(string))
                .HandleGet(async (qr, ct) =>
                {
                    var loggedinUser = qr.Session.User().Uid;
                    var isITshop = qr.Parameters.Get<string>("is_for_ITshop");
                    var tenantId = qr.Parameters.Get<string>("tenant_uid");

                    if (!String.IsNullOrEmpty(isITshop) && String.IsNullOrEmpty(tenantId))
                    {
                        if (isITshop == "0" || isITshop == "1")
                        {
                            var queryITshopGroups = Query.From("AADGroup")
                            .Select("DisplayName", "IsForITShop")
                            .Where(string.Format(@"IsForITShop = '{0}'", isITshop));

                            var tryGetITShopGroups = await qr.Session.Source().TryGetAsync(queryITshopGroups, EntityLoadType.DelayedLogic).ConfigureAwait(false);

                            if (tryGetITShopGroups.Success)
                            {
                                // Get the collection of entities matching queryITshopGroups
                                var FilteredGroups = await qr.Session.Source()
                                    .GetCollectionAsync(queryITshopGroups, EntityCollectionLoadType.Default, ct)
                                    .ConfigureAwait(false);

                                // Initialize a list to hold the response data
                                List<object> responseArray = new List<object>();

                                foreach (var group in FilteredGroups)
                                {
                                    // Retrieve the "DisplayName" value from the group, representing a functionality
                                    var groupName = await group.GetValueAsync<string>("DisplayName")
                                        .ConfigureAwait(false);

                                    responseArray.Add(groupName);
                                }
                                return responseArray;
                            }
                            else
                            {
                                return null;
                            }
                        }
                        else
                        {
                            // Error with a custom message and error code
                            throw new System.Web.HttpException(611, "Value of the parameter 'is_for_ITshop' should be 0 or 1.");
                        }
                    }
                    else if (String.IsNullOrEmpty(isITshop) && !String.IsNullOrEmpty(tenantId))
                    {
                        var queryGroupsWithTenants = Query.From("AADGroup")
                        .Select("*")
                        .Where(string.Format(@"UID_AADOrganization = '{0}'", tenantId));

                        var tryGetGroupsWithTenant = await qr.Session.Source().TryGetAsync(queryGroupsWithTenants, EntityLoadType.DelayedLogic).ConfigureAwait(false);

                        if (tryGetGroupsWithTenant.Success)
                        {
                            // Get the collection of entities matching query1
                            var FilteredGroups = await qr.Session.Source()
                                .GetCollectionAsync(queryGroupsWithTenants, EntityCollectionLoadType.Default, ct)
                                .ConfigureAwait(false);

                            // Initialize a list to hold the response data
                            List<object> responseArray = new List<object>();

                            foreach (var group in FilteredGroups)
                            {
                                // Retrieve the "DisplayName" value from the group, representing a functionality
                                var groupName = await group.GetValueAsync<string>("DisplayName")
                                    .ConfigureAwait(false);

                                responseArray.Add(groupName);
                            }
                            return responseArray;
                        }
                        else
                        {
                            return null;
                        }

                    }
                    else if (!String.IsNullOrEmpty(isITshop) && !String.IsNullOrEmpty(tenantId))
                    {
                        if (isITshop == "0" || isITshop == "1")
                        {
                            var queryGroupsforITShopWithTenant = Query.From("AADGroup")
                       .Select("*")
                       .Where(string.Format(@"UID_AADOrganization = '{0}' AND IsForITShop = '{1}'", tenantId, isITshop));

                            var tryGetGroupsforITShopWithTenant = await qr.Session.Source().TryGetAsync(queryGroupsforITShopWithTenant, EntityLoadType.DelayedLogic).ConfigureAwait(false);

                            if (tryGetGroupsforITShopWithTenant.Success)
                            {
                                // Get the collection of entities matching query1
                                var FilteredGroups = await qr.Session.Source()
                                    .GetCollectionAsync(queryGroupsforITShopWithTenant, EntityCollectionLoadType.Default, ct)
                                    .ConfigureAwait(false);

                                // Initialize a list to hold the response data
                                List<object> responseArray = new List<object>();

                                foreach (var group in FilteredGroups)
                                {
                                    // Retrieve the "DisplayName" value from the group, representing a functionality
                                    var groupName = await group.GetValueAsync<string>("DisplayName")
                                        .ConfigureAwait(false);

                                    responseArray.Add(groupName);
                                }
                                return responseArray;
                            }
                            else
                            {
                                return null;
                            }
                        }
                        else
                        {
                            throw new System.Web.HttpException(611, "Value of the parameter 'is_for_ITshop' should be 0 or 1.");
                        }
                    }
                    else
                    {
                        return null;
                    }
                }));



            // Ex 04 - assign AADGroup to AAD user
            builder.AddMethod(Method.Define("api_exercises/aad_requests/assign_aad_group")
                .Handle<PostedAADGroupMembership, string>("POST", async (posted, qr, ct) =>
                {
                    var uid_loggedinUser = qr.Session.User().Uid;
                    var aad_username = posted.aad_username;
                    var uid_aad_group = posted.uid_aad_group;
                    string uid_person_to_assign_group = "";
                    string uid_org = "";

                    // Create a query to find the uid_person of the user which is about to assigned the AADGroup
                    var q1 = Query.From("AADUser")
                              .Select("UID_Person")
                              .Where(string.Format("UserPrincipalName = '{0}'", aad_username));

                    var tryGetUidPerson = await qr.Session.Source().TryGetAsync(q1, EntityLoadType.DelayedLogic).ConfigureAwait(false);

                    // Create a query to find the uid_Org which is needed in order to make the insert on PersonWantsOrg successfully
                    var queryUIDOrg = Query.From("ITShopOrg")
                     .Select("UID_ITShopOrg")
                     .Where(string.Format(@"UID_AccProduct in (select UID_AccProduct from AADGroup 
                                               where UID_AADGroup = '{0}')", uid_aad_group));

                    var tryGetUidProduct = await qr.Session.Source().TryGetAsync(queryUIDOrg, EntityLoadType.DelayedLogic).ConfigureAwait(false);


                    if (tryGetUidPerson.Success && tryGetUidProduct.Success)
                    {
                        uid_person_to_assign_group = tryGetUidPerson.Result.GetValue<string>("UID_Person");
                        uid_org = tryGetUidProduct.Result.GetValue<string>("UID_ITShopOrg");

                        // Create a new 'PersonWantsOrg' entity
                        var newPWOAssignment = await qr.Session.Source().CreateNewAsync("PersonWantsOrg",
                        new EntityParameters
                        {
                            CreationType = EntityCreationType.DelayedLogic
                        }, ct).ConfigureAwait(false);

                        // Set the values for the new 'PersonWantsOrg' entity
                        await newPWOAssignment.PutValueAsync("UID_PersonInserted", uid_loggedinUser, ct).ConfigureAwait(false);
                        await newPWOAssignment.PutValueAsync("UID_PersonOrdered", uid_person_to_assign_group, ct).ConfigureAwait(false);
                        await newPWOAssignment.PutValueAsync("UID_Org", uid_org, ct).ConfigureAwait(false);

                        // Start Unit of Work to save the new entity to the database
                        using (var u = qr.Session.StartUnitOfWork())
                        {
                            await u.PutAsync(newPWOAssignment, ct).ConfigureAwait(false);  // Add the new entity to the unit of work
                            await u.CommitAsync(ct).ConfigureAwait(false);  // Commit the transaction to persist changes
                        }

                        // Return a successful response
                        return $"Assignment of AAD Group '{uid_aad_group}' to '{posted.aad_username}' completed successfully.";
                    }
                    else
                    {
                        throw new System.Web.HttpException(504, $"An error occured. AAD Group '{uid_aad_group}' failed to be assigned to user '{posted.aad_username}'.");
                    }

                }));


            // Ex 05 - Predefined SQL
            // Designer -> Base data -> Advanced -> Predifined SQL
            // to assign permission group to predifined SQL: Object Browser -> QBMGroupHasLimitedSQL -> Insert -> Foreign keys edit
            //select p.FirstName, p.LastName, dd.FullPath from person p
            //left join Department dd on dd.UID_Department = p.UID_Department
            //where p.PersonalTitle = 'Software Tester'
            //and p.FirstName Like 'K%'
            //and p.Description = 'IdentityAdmins'


            // Add a POST method named "api_exercises/exe_predefined_sql" to the API
            builder.AddMethod(Method.Define("api_exercises/exe_predefined_sql")
                  // Request type of PostedDataPredefSQL, Response type of List<List<ColumnData>>
                  .Handle<PostedDataPredefSQL, List<List<ColumnData>>>("POST", async (posted, qr, ct) =>
                  {
                      // Retrieve the UID of the currently logged-in user from the session
                      var strUID_Person = qr.Session.User().Uid;

                      // Initialize a list to hold the results, where each result is a list of ColumnData objects
                      var results = new List<List<ColumnData>>();

                      // Resolve an instance of IStatementRunner from the session
                      var runner = qr.Session.Resolve<IStatementRunner>();

                      // Execute a predefined SQL statement named "CCC_Person_with_PersonalTitle_and_Description" with parameters
                      using (var reader = runner.SqlExecute("CCC_Person_with_PersonalTitle_and_Desc", new[]
                      {
                          // Create a query parameter named "person_title" with the value of the posted PersonalTitle
                          QueryParameter.Create("person_title", posted.PersonTitle),
                          // Create a query parameter named "person_description" with the value of the posted Description
                          QueryParameter.Create("person_description", posted.PersonDescription),
                          // Create a query parameter named "starts_with" with the value of the starting char of the firstname requested
                          QueryParameter.Create("starts_with", posted.NameStartsWith)
                      })) 
                      {
                          // Read each row returned by the SQL query
                          while (reader.Read())
                          {
                              // Create a list of ColumnData objects for each column in the row
                              var row = new List<ColumnData>
                              {
                                  // Map the "FirstName" column
                                  new ColumnData
                                  {
                                      Column = "First Name",
                                      Value = reader["First Name"].ToString()
                                  },
                                  // Map the "LastName" column
                                  new ColumnData
                                  {
                                      Column = "Last Name",
                                      Value = reader["Last Name"].ToString()
                                  },
                                  // Map the "Department" column
                                  new ColumnData
                                  {
                                      Column = "Department",
                                      // Return the Department's fullPath - if the value is NULL returns "No department found"
                                      Value = reader["Department"]?.ToString() ?? "No department found"
                                  }
                              };

                              // Add the row to the results list
                              results.Add(row);
                          }
                      }

                      // Return the results as an array
                      return results;

                  }));


            // Add a POST method named "api_exercises/exe_predefined_sql_dictionary" to the API
            builder.AddMethod(Method.Define("api_exercises/exe_predefined_sql_dictionary")
                // Request type of PostedDataPredefSQL, Response type of Dictionary<string, List<ColumnData>>
                .Handle<PostedDataPredefSQL, Dictionary<string, List<ColumnData>>>("POST", async (posted, qr, ct) =>
                {
                    // Retrieve the UID of the currently logged-in user from the session
                    var strUID_Person = qr.Session.User().Uid;

                    // Initialize a dictionary to hold the results, where each result is a list of ColumnData objects
                    var results = new Dictionary<string, List<ColumnData>>();

                    // Resolve an instance of IStatementRunner from the session
                    var runner = qr.Session.Resolve<IStatementRunner>();

                    // Execute a predefined SQL statement named "CCC_Person_with_PersonalTitle_and_Description" with parameters
                    using (var reader = runner.SqlExecute("CCC_Person_with_PersonalTitle_and_Desc", new[]
                    {
                        // Create a query parameter named "person_title" with the value of the posted PersonalTitle
                        QueryParameter.Create("person_title", posted.PersonTitle),
                        // Create a query parameter named "person_description" with the value of the posted Description
                        QueryParameter.Create("person_description", posted.PersonDescription),
                        // Create a query parameter named "starts_with" with the value of the starting char of the firstname requested
                        QueryParameter.Create("starts_with", posted.NameStartsWith)
                    }))
                    {
                        // Read each row returned by the SQL query
                        while (reader.Read())
                        {
                            // Create a list of ColumnData objects for each column in the row
                            var row = new List<ColumnData>
                            {
                                // Map the "FirstName" column
                                new ColumnData
                                {
                                    Column = "First Name",
                                    Value = reader["First Name"].ToString()
                                },
                                // Map the "LastName" column
                                new ColumnData
                                {
                                    Column = "Last Name",
                                    Value = reader["Last Name"].ToString()
                                },
                                // Map the "Department" column
                                new ColumnData
                                {
                                    Column = "Department",
                                    // Return the Department's fullPath - if the value is NULL returns "No department found"
                                    Value = reader["Department"]?.ToString() ?? "No department found"
                                }
                            };

                            // Use a unique key for each row, in this case the UID_Person of the related user
                            var key = reader["UID_Person"].ToString();

                            // Add the row to the results dictionary
                            results[key] = row;
                        }
                    }

                    // Return the results as a dictionary
                    return results;
                }));


        }
        

        // The ColumnData class represents a single column and its value in a database row
        public class ColumnData
        {
            // The name of the column
            public string Column { get; set; }
            // The value of the column in the current row
            public string Value { get; set; }
        }

        public class PostedDataPredefSQL 
        { 
            public string PersonTitle { get; set; }
            public string PersonDescription { get; set; }
            public string NameStartsWith { get; set; }

        }


        public class PostedAADGroupMembership
        {
            public string aad_username { get; set; }
            public string uid_aad_group { get; set; }

        }

        // JSON Object structure for body request to DELETE an AAD Group
        public class PostedAADGroup_UID
        {
            public string uid_aad_group { get; set; }
        }

        // The ReturnedDetails class represents the structure of the data returned to the client
        public class ReturnedDetails
        {
            // The UID of the deleted AAD Group
            public string AAD_UID_GROUP { get; set; }

            // Property to hold any error message
            public string errormessage { get; set; }
            public string successmessage { get; set; }
            public string DisplayName { get; set; }

            // Static method to create a ReturnedDetails instance from an IEntity object
            public static async Task<ReturnedDetails> fromEntity(IEntity entity, ISession session)
            {
                string DisplayName = await entity.GetValueAsync<string>("DisplayName").ConfigureAwait(false);
                // Asynchronously get the UID_AADGroup value from the entity and assign it
                var AAD_UID_GROUP = await entity.GetValueAsync<string>("UID_AADGroup").ConfigureAwait(false);
                // Instantiate a new ReturnedDetails object
                var g = new ReturnedDetails
                {
                    successmessage = $"Azure Active Directory Group: '{DisplayName}' with UID_AADGroup '{AAD_UID_GROUP}' deleted successfully."
                };

                // Return the populated ReturnedDetails object
                return g;
            }

            // Static method to return a ReturnedDetails instance containing an error message
            public static async Task<ReturnedDetails> ReturnObject(string data)
            {
                // Instantiate a new ReturnedClass object with the error message
                var x = new ReturnedDetails
                {
                    errormessage = data
                };

                // Return the error-containing ReturnedDetails object
                return x;
            }

            // Static method to throw an HTTP exception in case of an error
            // Parameters:
            // - mess: The error message to be displayed
            // - errorNumber: The HTTP error code corresponding to the error
            public static async Task<ReturnedDetails> Error(string mess, int errorNumber)
            {
                // Throw an HTTP exception with the provided error number and message
                throw new System.Web.HttpException(errorNumber, mess);
            }
        }

        // JSON Object structure for body request to update an AAD Group
        public class PostedAADGroupToUpdate
        {
            public string uid_aadGroup { get; set; }
            public string display_name { get; set; }
            public string UIDOrganization_tenant { get; set; }
            public string group_alias { get; set; }
            public string group_description { get; set; }

        }


        // JSON Object structure for body request to create an AAD Group
        public class PostedAADGroup
        {
            public string display_name { get; set; }
            public string UIDOrganization_tenant { get; set; }
            public string group_alias { get; set; }
            public string group_description { get; set; }
        }

   
        // JSON Object structure for GET response
        public class ReturnedAADGroupDetails
        {
            // Properties to hold the fields of the requested AAD Group

            public string UID_AADGroup { get; set; }
            public string DisplayName { get; set; }
            public string Description { get; set; }
            public string IsForITShop { get; set; }
            public string MailNickName { get; set; }


            // Static method to create a ReturnedAADGroupDetails instance from an IEntity object
            public static async Task<ReturnedAADGroupDetails> fromEntity(IEntity entity, ISession session)
            {
                // Instantiate a new ReturnedName object and populate it with data from the entity
                var g = new ReturnedAADGroupDetails
                {
                    // Asynchronously get the UID_AADGroup value from the entity
                    UID_AADGroup = await entity.GetValueAsync<string>("UID_AADGroup").ConfigureAwait(false),

                    // Asynchronously get the DisplayName value from the entity
                    DisplayName = await entity.GetValueAsync<string>("DisplayName").ConfigureAwait(false),

                    // Asynchronously get the Description value from the entity
                    Description = await entity.GetValueAsync<string>("Description").ConfigureAwait(false),

                    // Asynchronously get the IsForITShop value from the entity
                    IsForITShop = await entity.GetValueAsync<string>("IsForITShop").ConfigureAwait(false),

                    // Asynchronously get the MailNickName value from the entity
                    MailNickName = await entity.GetValueAsync<string>("MailNickName").ConfigureAwait(false),
                };

                // Return the populated ReturnedAADGroupDetails object
                return g;
            }
        }

        public class PostedMessage
        {
            public string Input { get; set; }
        }

        public class DataObject
        {
            public string Message { get; set; }
        }


    }
}


