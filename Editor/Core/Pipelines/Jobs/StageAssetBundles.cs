using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ThunderKit.Core.Attributes;
using System.Threading.Tasks;
using ThunderKit.Core.Data;
using ThunderKit.Core.Manifests;
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
        [EnumFlag] public BuildAssetBundleOptions AssetBundleBuildOptions =
            BuildAssetBundleOptions.UncompressedAssetBundle;

        public BuildTarget buildTarget = BuildTarget.StandaloneWindows;
        public bool simulate;
        public bool copyManifest = true;

        [PathReferenceResolver] public string BundleArtifactPath = "<AssetBundleStaging>";

        public override Task Execute(Pipeline pipeline)
        {
            AssetDatabase.SaveAssets();
            EditorUserBuildSettings.SwitchActiveBuildTarget(
                BuildPipeline.GetBuildTargetGroup(buildTarget),
                buildTarget);
            var manifests = pipeline.Manifests;
            var abds = new List<AssetBundleDefinitions>();
            foreach (var t in manifests) abds.AddRange(t.Data.OfType<AssetBundleDefinitions>());

            var assetBundleDefs = abds.ToArray();
            var hasValidBundles = assetBundleDefs.Any(abd => abd.assetBundles.Any(ab => !string.IsNullOrEmpty(ab)));
            if (!hasValidBundles)
            {
                var scriptPath =
                    UnityWebRequest.EscapeURL(AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(this)));
                pipeline.Log(LogLevel.Warning,
                    $"No valid AssetBundleDefinitions defined, skipping [{nameof(StageAssetBundles)}](assetlink://{scriptPath}) PipelineJob");
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
                pipeline.Log(LogLevel.Information, $"Building AssetBundles to {dir}");
                BuildPipeline.BuildAssetBundles(dir, AssetBundleBuildOptions, buildTarget);
                foreach (var bundles in abds)
                {
                    pipeline.Log(LogLevel.Verbose, "bundle definition iteration");
                    foreach (var bundle in bundles.assetBundles)
                    {
                        pipeline.Log(LogLevel.Verbose, "bundle iteration");
                        foreach (var outputPath in bundles.StagingPaths.Select(path => path.Resolve(pipeline, this)))
                        {
                            pipeline.Log(LogLevel.Verbose, "output path iteration");
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
                        
                        if (!string.IsNullOrEmpty(BundleArtifactPath))
                        {
                            var orig = Path.Combine(dir, bundle);
                            var dest = Path.Combine(bundleArtifactPath, bundle);
                            pipeline.Log(LogLevel.Information, $"Copying {orig} to {dest}");
                            FileUtil.ReplaceFile(orig, dest);
                            if (copyManifest)
                            {
                                orig = Path.Combine(dir, bundle + ".manifest");
                                dest = Path.Combine(bundleArtifactPath, bundle + ".manifest");
                                pipeline.Log(LogLevel.Information, $"Copying {orig} to {dest}");
                                FileUtil.ReplaceFile(orig, dest);
                            }
                        }
                    }
                }

                pipeline.ManifestIndex = -1;
            }

            return Task.CompletedTask;
        }
    }
}