using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Unity.Simulation.Client
{
    /// <summary>
    /// The API class encapsulates the REST API used by the Unity Simulation service.
    /// </summary>
    public static class API
    {
        // Public Members

#if !UNITY_EDITOR
        /// <summary>
        /// Authenticates the currently active project with the Unity Simulation service.
        /// </summary>
        public static void Login()
        {
            Token.Load();

            var redirect_uri   = $"http://127.0.0.1:{Config.kDefaultRedirectUriPort}/v1/auth/login/callback";
            var usim_oauth_url = $"{Config.apiEndpoint}/v1/auth/login?redirect_uri={redirect_uri}";

            var httpListener = new HttpListener();
            httpListener.Prefixes.Add(redirect_uri + "/");
            httpListener.Start();

            Application.OpenURL(usim_oauth_url);

            var context  = httpListener.GetContext();
            var request  = context.Request;
            var token    = new Token(Config.apiEndpoint, request.QueryString);
            token.Save(Config.tokenFile);

            var text = Encoding.UTF8.GetBytes("You have been successfully authenticated to use Unity Simulation services.");
            context.Response.OutputStream.Write(text, 0, text.Length);
            context.Response.Close();
            httpListener.Stop();
        }

        /// <summary>
        /// Refreshes the auth token for the currently active project with the Unity Simulation service.
        /// </summary>
        public static void Refresh()
        {
            var token = Token.Load(refreshIfExpired: false);
            token.Refresh();
            token.Save(Config.tokenFile);
        }
#endif//UNITY_EDITOR


        public struct LoginRequest
        {
            public string Username;
            public string Password;
        }

        public static string Login(string username, string password)
        {
            var url = $"{Config.apiEndpoint}/v1/auth/login";

            using (var message = new HttpRequestMessage(HttpMethod.Post, url))
            {
                var body = new LoginRequest()
                {
                    Username = username,
                    Password = password
                };
                
                message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json" ));
                message.Content = new StringContent(JsonUtility.ToJson(body), Encoding.UTF8, "application/json");
                
                var request = Transaction.client.SendAsync(message);
                request.Wait(TimeSpan.FromSeconds(Config.timeoutSecs));

                var payload = request.Result.Content.ReadAsStringAsync();
                payload.Wait();
                var data = JsonUtility.FromJson<TokenData>(payload.Result);
                return data.access_token;
            }
        }

        /// <summary>
        /// Retrieves the supported SysParams for the Unity Simulation service.
        /// </summary>
        /// <returns> Array of SysParamDefinition </returns>
        public static SysParamDefinition[] GetSysParams(string accessToken = null)
        {
            Project.ValidateProjectId();
            var url = $"{Config.apiEndpoint}/v1/projects/{Project.activeProjectId}/sys_params";
            using (var message = new HttpRequestMessage(HttpMethod.Get, url))
            {
                Transaction.SetAuthHeaders(message.Headers, accessToken);
                var request = Transaction.client.SendAsync(message);
                request.Wait(TimeSpan.FromSeconds(Config.timeoutSecs));

                if (!request.Result.IsSuccessStatusCode)
                {
                    throw new Exception("GetSysParams failed " + request.Result.ReasonPhrase);
                }

                var payload = request.Result.Content.ReadAsStringAsync();
                payload.Wait();

                return JsonUtility.FromJson<SysParamArray>(payload.Result).sys_params;
            }
        }

        /// <summary>
        /// Uploads a build to the Unity Simulation Service.
        /// Note that the executable name must end with .x86_64, and the entire build must be zipped into a single archive.
        /// </summary>
        /// <param name="name"> Name for the build when uploaded. </param>
        /// <param name="location"> Path to the zipped archive. </param>
        /// <returns> Uploaded build id. </returns>
        public static string UploadBuild(string name, string location, string accessToken = null)
        {
            Project.ValidateProjectId();
            return Transaction.Upload($"{Config.apiEndpoint}/v1/projects/{Project.activeProjectId}/builds", name, location, true, accessToken);
        }

        /// <summary>
        /// Uploads a build to the Unity Simulation Service.
        /// Note that the executable name must end with .x86_64, and the entire build must be zipped into a single archive.
        /// </summary>
        /// <param name="name"> Name for the build when uploaded. </param>
        /// <param name="location"> Path to the zipped archive. </param>
        /// <param name="accessToken"> Access token to use, null will use the project access token. </param>
        /// <param name="progress"> Action to perform for progress updates. </param>
        /// <returns> Uploaded build id. </returns>
        public static Task<string> UploadBuildAsync(string name, string location, string accessToken = null, string contentType = null, CancellationTokenSource cancellationTokenSource = null, Action<float> progress = null)
        {
            return Transaction.UploadAsync($"{Config.apiEndpoint}/v1/projects/{Project.activeProjectId}/builds", name, location, accessToken, contentType, cancellationTokenSource, progress);
        }

        /// <summary>
        /// Download a build from Unity Simulation to a specific location.
        /// </summary>
        /// <param name="id"> Build upload id to download. </param>
        /// <param name="location"> Path to where you want the download to be saved. </param>
        /// <returns> Array of SysParamDefinition </returns>
        public static void DownloadBuild(string id, string location, string accessToken = null)
        {
            Project.ValidateProjectId();
            Transaction.Download($"{Config.apiEndpoint}/v1/projects/{Project.activeProjectId}/builds", id, location, true, accessToken);
        }

        /// <summary>
        /// Serialize a struct and upload the JSON as an app param.
        /// </summary>
        /// <param name="name"> Name for uploaded resource. </param>
        /// <param name="location"> Struct value to be serialized and uploaded. </param>
        /// <returns> Uploaded app param id. </returns>
        public static string UploadAppParam<T>(string name, T param, string accessToken = null)
        {
            Project.ValidateProjectId();
            return Transaction.Upload($"{Config.apiEndpoint}/v1/projects/{Project.activeProjectId}/app_params", name, Encoding.UTF8.GetBytes(JsonUtility.ToJson(param)), true, accessToken);
        }
        
        /// <summary>
        /// Serialize a struct and upload the JSON as an app param.
        /// </summary>
        /// <param name="name"> Name for uploaded resource. </param>
        /// <param name="appParamString"> AppParam Json string </param>
        /// <returns> Uploaded app param id. </returns>
        public static string UploadAppParam(string name, string appParamString, string accessToken = null)
        {
            return Transaction.Upload($"{Config.apiEndpoint}/v1/projects/{Project.activeProjectId}/app_params", name, Encoding.UTF8.GetBytes(appParamString), true, accessToken);
        }

        /// <summary>
        /// Download an app param from Unity Simulation to a specific location.
        /// </summary>
        /// <param name="id"> App param upload id to download. </param>
        /// <param name="location"> Path to where you want the download to be saved. </param>
        /// <returns> Copy of struct value. </returns>
        public static T DownloadAppParam<T>(string id, string accessToken = null)
        {
            Project.ValidateProjectId();
            return JsonUtility.FromJson<T>(Encoding.UTF8.GetString(Transaction.Download($"{Config.apiEndpoint}/v1/projects/{Project.activeProjectId}/app_params", id, true, accessToken)));
        }

        /// <summary>
        /// Upload a run definition to Unity Simulation.
        /// </summary>
        /// <param name="definition"> Run definition to be uploaded. </param>
        /// <returns> Uploaded run definition id. </returns>
        public static string UploadRunDefinition(RunDefinition definition, string accessToken = null)
        {
            Project.ValidateProjectId();
            var url = $"{Config.apiEndpoint}/v1/projects/{Project.activeProjectId}/run_definitions";
            using (var message = new HttpRequestMessage(HttpMethod.Post, url))
            {
                Transaction.SetAuthHeaders(message.Headers, accessToken);
                message.Content = new StringContent(JsonUtility.ToJson(definition), Encoding.UTF8, "application/json");
                var request = Transaction.client.SendAsync(message);
                request.Wait(TimeSpan.FromSeconds(Config.timeoutSecs));

                if (!request.Result.IsSuccessStatusCode)
                {
                    throw new Exception("UploadRunDefinition failed " + request.Result.ReasonPhrase);
                }

                var payload = request.Result.Content.ReadAsStringAsync();
                payload.Wait();

                var response = JsonUtility.FromJson<RunDefinitionId>(payload.Result);
                return response.definition_id;
            }
        }

        /// <summary>
        /// Download a run definition from Unity Simulation.
        /// </summary>
        /// <param name="id"> Run definition upload id to download. </param>
        /// <returns> RunDefinition struct value. </returns>
        public static RunDefinition DownloadRunDefinition(string definitionId, string accessToken = null)
        {
            Project.ValidateProjectId();
            using (var message = new HttpRequestMessage(HttpMethod.Get, $"{Config.apiEndpoint}/v1/projects/{Project.activeProjectId}/run_definitions/{definitionId}"))
            {
                Transaction.SetAuthHeaders(message.Headers, accessToken);
                var request = Transaction.client.SendAsync(message);
                request.Wait(TimeSpan.FromSeconds(Config.timeoutSecs));

                if (!request.Result.IsSuccessStatusCode)
                {
                    throw new Exception("DownloadRunDefinition failed " + request.Result.ReasonPhrase);
                }

                var payload = request.Result.Content.ReadAsStringAsync();
                payload.Wait();

                return JsonUtility.FromJson<RunDefinition>(payload.Result);
            }
        }

        /// <summary>
        /// Download a summary of a run execution from Unity Simulation.
        /// </summary>
        /// <param name="id"> Execution id to summarize. </param>
        /// <returns> RunSummary struct value. </returns>
        public static RunSummary Summarize(string executionId, string accessToken = null)
        {
            Project.ValidateProjectId();
            var url = $"{Config.apiEndpoint}/v1/projects/{Project.activeProjectId}/runs/{executionId}/summary";

            using (var message = new HttpRequestMessage(HttpMethod.Get, url))
            {
                Transaction.SetAuthHeaders(message.Headers, accessToken);
                var request = Transaction.client.SendAsync(message);
                request.Wait(TimeSpan.FromSeconds(Config.timeoutSecs));

                if (!request.Result.IsSuccessStatusCode)
                {
                    throw new Exception("Summarize failed " + request.Result.ReasonPhrase);
                }

                var payload = request.Result.Content.ReadAsStringAsync();
                payload.Wait();

                return JsonUtility.FromJson<RunSummary>(payload.Result);
            }
        }

        /// <summary>
        /// Download a description of a run exection from Unity Simulation.
        /// </summary>
        /// <param name="id"> Execution id to describe. </param>
        /// <returns> RunDescription struct value. </returns>
        public static RunDescription Describe(string executionId, string accessToken = null)
        {
            Project.ValidateProjectId();
            var url = $"{Config.apiEndpoint}/v1/projects/{Project.activeProjectId}/runs/{executionId}";
            using (var message = new HttpRequestMessage(HttpMethod.Get, url))
            {
                Transaction.SetAuthHeaders(message.Headers, accessToken);
                var request = Transaction.client.SendAsync(message);
                request.Wait(TimeSpan.FromSeconds(Config.timeoutSecs));

                if (!request.Result.IsSuccessStatusCode)
                {
                    throw new Exception("Describe failed " + request.Result.ReasonPhrase);
                }

                var payload = request.Result.Content.ReadAsStringAsync();
                payload.Wait();

                return JsonUtility.FromJson<RunDescription>(payload.Result);
            }
        }

        /// <summary>
        /// Download the manifest of uploaded artifacts for a run exection.
        /// </summary>
        /// <param name="executionId"> Execution id whose manifest you wish to download. </param>
        /// <returns> Dictionary of entry hash code mapped to ManifestEntry. </returns>
        /// <remarks> You can call this at any time, and multiple times, and the dictionary will contain new items that have been uploaded.</remarks>
        public static Dictionary<int, ManifestEntry> GetManifest(string executionId, string accessToken = null)
        {
            Project.ValidateProjectId();

            var entries = new Dictionary<int, ManifestEntry>();

            var url = $"{Config.apiEndpoint}/v1/projects/{Project.activeProjectId}/runs/{executionId}/data";
            using (var message = new HttpRequestMessage(HttpMethod.Get, url))
            {
                Transaction.SetAuthHeaders(message.Headers, accessToken);
                var request = Transaction.client.SendAsync(message);
                request.Wait(TimeSpan.FromSeconds(Config.timeoutSecs));

                if (!request.Result.IsSuccessStatusCode)
                {
                    throw new Exception($"Unable to get manifest {request.Result.ReasonPhrase}.");
                }

                var payload = request.Result.Content.ReadAsStringAsync();
                payload.Wait();

                var lines = payload.Result.Split(Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);

                for (var i = 1; i < lines.Length; ++i)
                {
                    var l = lines[i].Split(',');
                    Debug.Assert(l.Length == 6);
                    ManifestEntry e;
                    e.executionId = l[0];
                    e.appParamId  = l[1];
                    e.instanceId  = int.Parse(l[2]);
                    e.attemptId   = int.Parse(l[3]);
                    e.fileName    = l[4];
                    e.downloadUri = l[5];
                    entries.Add(e.GetHashCode(), e);
                }
            }

            return entries;
        }
    }
}
