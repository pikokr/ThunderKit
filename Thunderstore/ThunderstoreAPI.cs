﻿#if UNITY_EDITOR
using ThunderKit.Core.Editor;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace ThunderKit.Thunderstore
{
    /// <summary>
    /// ThunderstoreAPI provides an interface to the Thunderstore API
    /// Currently supports Listing, Downloading, and Searching for packages.
    /// </summary>
    public class ThunderstoreAPI
    {
        const string ThunderstoreIO = "https://thunderstore.io";
        const string PackageListApi = ThunderstoreIO + "/api/v1/package";

        internal static List<Package> loadedPackages = new List<Package>();

        [MenuItem(Core.Constants.ThunderKitMenuRoot + "Refresh Thunderstore", priority = Core.Constants.ThunderKitMenuPriority)]
        [InitializeOnLoadMethod]
        public static async void LoadPages()
        {
            loadedPackages.Clear();
            using (WebClient client = new WebClient())
            {
                var address = new Uri(PackageListApi);
                var response = await client.DownloadStringTaskAsync(address);
                var resultSet = JsonUtility.FromJson<PackagesResponse>($"{{ \"{nameof(PackagesResponse.results)}\": {response} }}");
                loadedPackages.AddRange(resultSet.results);
            }
        }

        public static IEnumerable<Package> LookupPackage(string name, int pageIndex = 1, bool logStart = true) => loadedPackages.Where(package => IsMatch(package, name)).ToArray();

        static bool IsMatch(Package package, string name)
        {
            CompareInfo comparer = CultureInfo.CurrentCulture.CompareInfo;
            var compareOptions = CompareOptions.IgnoreCase;
            var nameMatch = comparer.IndexOf(package.name, name, compareOptions) >= 0;
            var fullNameMatch = comparer.IndexOf(package.full_name, name, compareOptions) >= 0;

            var latest = package.versions.OrderByDescending(pck => pck.version_number).First();
            var latestFullNameMatch = comparer.IndexOf(latest.full_name, name, compareOptions) >= 0;
            return nameMatch || fullNameMatch || latestFullNameMatch;
        }

        public static Task<string> DownloadPackageAsync(Package package, string filePath)
        {
            using (WebClient WebClient = new WebClient())
            {
                var latest = package.versions.OrderByDescending(pck => pck.version_number).First();

                return WebClient.DownloadFileTaskAsync(latest.download_url, filePath).ContinueWith(t => filePath);
            }
        }

        public static void DownloadPackage(Package package, string filePath)
        {
            var latest = package.versions.OrderByDescending(pck => pck.version_number).First();
            var webRequest = UnityWebRequest.Get(latest.download_url);
            var asyncOpRequest = webRequest.SendWebRequest();
            void Request_completed(AsyncOperation obj)
            {
                if (webRequest.isNetworkError || webRequest.isHttpError)
                    Debug.Log(webRequest.error);
                else
                    System.IO.File.WriteAllBytes(Path.ChangeExtension(filePath, "dl"), webRequest.downloadHandler.data);

                if (File.Exists(filePath)) File.Delete(filePath);
                File.Move(Path.ChangeExtension(filePath, "dl"), filePath);
            }
            asyncOpRequest.completed += Request_completed;
        }
    }
}
#endif