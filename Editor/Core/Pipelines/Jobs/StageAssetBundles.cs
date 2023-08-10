using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ThunderKit.Core.Attributes;
using System.Threading.Tasks;
using ThunderKit.Core.Data;
using ThunderKit.Core.Manifests.Datums;
using ThunderKit.Core.Paths;
using ThunderKit.Core.Pipelines;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
using UnityEngine.Networking;

namespace ThunderKit.Pipelines.Jobs
{
    [PipelineSupport(typeof(Pipeline)), RequiresManifestDatumType(typeof(AssetBundleDefinitions))]
    public class StageAssetBundles : PipelineJob
    {
        [EnumFlag]
        public BuildAssetBundleOptions AssetBundleBuildOptions = BuildAssetBundleOptions.UncompressedAssetBundle;
        public BuildTarget buildTarget = BuildTarget.StandaloneWindows;
        public bool simulate;
        public bool copyManifest = true;

        [PathReferenceResolver]
        public string BundleArtifactPath = "<AssetBundleStaging>";
        public override Task Execute(Pipeline pipeline)
        {
            var excludedExtensions = new[] { ".dll", ".cs", ".meta" };

            AssetDatabase.SaveAssets();
            var manifests = pipeline.Manifests;
            var abdIndices = new Dictionary<AssetBundleDefinitions, int>();
            var abds = new List<AssetBundleDefinitions>();
            for (int i = 0; i < manifests.Length; i++)
                foreach (var abd in manifests[i].Data.OfType<AssetBundleDefinitions>())
                {
                    abds.Add(abd);
                    abdIndices.Add(abd, i);
                }

            var assetBundleDefs = abds.ToArray();
            var hasValidBundles = assetBundleDefs.Any(abd => abd.assetBundles.Any(ab => !string.IsNullOrEmpty(ab)));
            if (!hasValidBundles)
            {
                var scriptPath = UnityWebRequest.EscapeURL(AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(this)));
                pipeline.Log(LogLevel.Warning, $"No valid AssetBundleDefinitions defined, skipping [{nameof(StageAssetBundles)}](assetlink://{scriptPath}) PipelineJob");
                return Task.CompletedTask;
            }
            var bundleArtifactPath = BundleArtifactPath.Resolve(pipeline, this);
            Directory.CreateDirectory(bundleArtifactPath);

            if (!simulate)
            {
                var dir = $"Temp/ThunderKit/AssetBundles/{buildTarget}";
                if (Directory.Exists(dir))
                {
                    Directory.Delete(dir, true);
                }

                Directory.CreateDirectory(dir);
                BuildPipeline.BuildAssetBundles(dir, AssetBundleBuildOptions, buildTarget);
                for (pipeline.ManifestIndex = 0; pipeline.ManifestIndex < pipeline.Manifests.Length; pipeline.ManifestIndex++)
                {
                    var manifest = pipeline.Manifest;
                    foreach (var assetBundleDef in manifest.Data.OfType<AssetBundleDefinitions>())
                    {
                        foreach (var outputPath in assetBundleDef.StagingPaths.Select(path => path.Resolve(pipeline, this)))
                        {
                            foreach (var bundle in assetBundleDef.assetBundles)
                            {
                                var orig = Path.Combine(dir, bundle);
                                var dest = Path.Combine(outputPath, bundle);
                                pipeline.Log(LogLevel.Information, $"Copying {orig} to {dest}");
                                FileUtil.ReplaceFile(orig, dest);
                                if (copyManifest)
                                {
                                    orig = Path.Combine(dir, bundle + ".manifest");
                                    dest = Path.Combine(outputPath, bundle + ".manifest");
                                    pipeline.Log(LogLevel.Information, $"Copying {orig} to {dest}");
                                    FileUtil.ReplaceFile(orig, dest);
                                }
                            }
                        }
                    }
                }
                pipeline.ManifestIndex = -1;
            }

            return Task.CompletedTask;
        }

        private static void LogBundleDetails(StringBuilder logBuilder, AssetBundleBuild build)
        {
            logBuilder.AppendLine($"{build.assetBundleName}");
            foreach (var asset in build.assetNames)
            {
                var name = Path.GetFileNameWithoutExtension(asset);
                if (name.Length == 0) continue;
                logBuilder.AppendLine($"[{name}](assetlink://{UnityWebRequest.EscapeURL(asset)})");
                logBuilder.AppendLine();
            }

            logBuilder.AppendLine();
        }
    }
}
