#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

public class VRCHotswap
{
    public static SynchronizationContext syncContext = SynchronizationContext.Current;
    private static readonly string ProjTempPath = Application.temporaryCachePath;
    private static readonly string SysTempPath = Path.GetTempPath().Replace(@"\",@"/");
    private static readonly string ProjAssetsPath = Application.dataPath;
    private static AssetBundleRecompressOperation abro;

    [MenuItem("VRC Hotswap/Hotswap", true)]
    static bool ValidateHotswap()
    {
        if (!Application.isPlaying)
        {
            if (File.Exists(ProjTempPath + "/custom.vrca"))
            {
                File.Delete(ProjTempPath + "/custom.vrca");
            }
            return false;
        }
        var vrcsdk = GameObject.Find("VRCSDK");
        if (vrcsdk)
        {
            var scripts = vrcsdk.GetComponents<MonoBehaviour>();
            foreach (var script in scripts)
            {
                if (script.GetType().Name == "RuntimeBlueprintCreation")
                {
                    if (File.Exists(ProjTempPath + "/custom.vrca"))
                    {
                        return true;
                    }
                }
            }
        }
        return false;
    }

    [MenuItem("VRC Hotswap/Hotswap")]
    public static void Hotswap()
    {
        if (File.Exists(ProjTempPath + "/uncomp.vrca")) File.Delete(ProjTempPath + "/uncomp.vrca");
        if (File.Exists(ProjTempPath + "/uncomp2.vrca")) File.Delete(ProjTempPath + "/uncomp2.vrca");
        if (File.Exists(ProjTempPath + "/uncompD.vrca")) File.Delete(ProjTempPath + "/uncompD.vrca");
        if (File.Exists(ProjTempPath + "/custom2.vrca")) File.Delete(ProjTempPath + "/custom2.vrca");

        if (EditorUtility.DisplayDialog("Hotswap", "Please select the avatar file you want to hotswap", "Continue", "Cancel"))
        {
            string vrcapath = EditorUtility.OpenFilePanelWithFilters("Select VRCA File for Hotswap", "", new string[] { "Avatar Files", "vrca", "All files", "*" });

            if (string.IsNullOrEmpty(vrcapath))
            {
                Debug.LogWarning("Hotwap cancelled.\n");
                return;
            }
            Debug.Log("Selected file for Hotwap:\n" + vrcapath + "\n");

            abro = AssetBundle.RecompressAssetBundleAsync(vrcapath, ProjTempPath + "/uncomp.vrca", BuildCompression.Uncompressed);
            EditorUtility.DisplayProgressBar("Hotswap - Decompressing VRCA", "Decompressing Selected Avatar", 0.0f);
            EditorApplication.update += abroProgress;
            abro.completed += (AsyncOperation ao) =>
            {
                EditorApplication.update -= abroProgress;
                EditorUtility.ClearProgressBar();
                if (abro.success)
                {
                    HS2();
                }
                else
                {
                    Debug.LogError("Failed to decompress the selected VRCA file.\n" + abro.result + "\n");
                }
            };
        }
        else
        {
            Debug.LogWarning("Hotwap cancelled.\n");
        }
    }

    [MenuItem("VRC Hotswap/Get Latest VRC SDK")]
    public static void GetLatestSDK()
    {
        string vrcSDKs = "";
        try
        {
            using (WebClient wc = new WebClient())
            {
                wc.Headers.Add("User-Agent: Mozilla/5.0 (compatible; MSIE 9.0; Windows NT 6.1; WOW64; Trident/5.0)");
                vrcSDKs = wc.DownloadString(new Uri("https://api.vrchat.cloud/api/1/config"));
            }
        }
        catch (WebException e)
        {
            ErrorMessage("An error occurred while fetching latest VRC SDK3. VRChat API down?", e);
            return;
        }
        catch (NotSupportedException e)
        {
            ErrorMessage("Unexpected error occurred while fetching latest VRC SDK3.", e);
            return;
        }

        string pattern = @"https:\/\/files\.vrchat\.cloud\/sdk\/VRCSDK3-AVATAR-(([0-9]+\.*)+)_Public.unitypackage";
        Regex rg = new Regex(pattern);
        MatchCollection matchedSDKURL = rg.Matches(vrcSDKs);
        if (!(matchedSDKURL.Count == 1))
        {
            ErrorMessage("Couldn't parse latest VRC SDK3 version.");
            return;
        }
        string SDK3URL = matchedSDKURL[0].Value;
        string SDK3Ver = matchedSDKURL[0].Groups[1].Value;

        if (File.Exists(ProjAssetsPath + "/VRCSDK/version.txt"))
        {
            string installedversion = File.ReadLines(ProjAssetsPath + "/VRCSDK/version.txt").First().Trim();
            int canupdate = CanUpdate(installedversion, SDK3Ver);
            if (canupdate == 0)
            {
                Debug.Log($"<color=cyan>VRC SDK is up to date! Current is {installedversion}</color>\n"); return;
            }
            else if (canupdate == -1)
            {
                Debug.LogWarning($"Installed VRC SDK is ahead of Public release? Latest is {SDK3Ver}, installed is {installedversion}\n"); return;
            }
        }

        string SDK3Filepath = SysTempPath + "SDK3Avatars_" + SDK3Ver + ".unitypackage";
        if (!File.Exists(SDK3Filepath))
        {
            try
            {
                using (WebClient wc = new WebClient())
                {
                    wc.Headers.Add("User-Agent: Mozilla/5.0 (compatible; MSIE 9.0; Windows NT 6.1; WOW64; Trident/5.0)");
                    wc.DownloadFile(new Uri(SDK3URL), SDK3Filepath);
                }
            }
            catch (WebException e)
            {
                ErrorMessage($"An error occurred while downloading latest VRC SDK3 (v{SDK3Ver}). Internet down?", e);
                return;
            }
            catch (NotSupportedException e)
            {
                ErrorMessage($"Unexpected error occurred while downloading latest VRC SDK3 (v{SDK3Ver}).", e);
                return;
            }
        }

        AssetDatabase.ImportPackage(SDK3Filepath, true);
    }

    [MenuItem("VRC Hotswap/Get Custom Preview Images", true)]
    static bool Validate_HasVRCSDK()
    {
#if VRC_SDK_VRCSDK3
        if (File.Exists(ProjAssetsPath + "/VRCSDK/Plugins/VRCSDK3A.dll"))
        {
            return true;
        }
#endif
        return false;
    }

    [MenuItem("VRC Hotswap/Get Custom Preview Images")]
    public static void GetCustomPreviewImages()
    {
        string CustomPrevImagesFilepath = SysTempPath + "VRC Custom Preview Images.unitypackage";
        try
        {
            using (WebClient wc = new WebClient())
            {
                wc.Headers.Add("User-Agent: Mozilla/5.0 (compatible; MSIE 9.0; Windows NT 6.1; WOW64; Trident/5.0)");
                wc.DownloadFile(new Uri("https://github.com/FACS01-01/FACS_Utilities/raw/main/Plugins/VRC%20Custom%20Preview%20Images.unitypackage"), CustomPrevImagesFilepath+"2");
            }
        }
        catch (WebException e)
        {
            ErrorMessage($"An error occurred while downloading FACS Custom Preview Images. GitHub API down?", e); return;
        }
        catch (NotSupportedException e)
        {
            ErrorMessage($"Unexpected error occurred while downloading FACS Custom Preview Images.", e); return;
        }
        if (File.Exists(CustomPrevImagesFilepath)) File.Delete(CustomPrevImagesFilepath);
        File.Move(CustomPrevImagesFilepath + "2", CustomPrevImagesFilepath);
        AssetDatabase.ImportPackage(CustomPrevImagesFilepath, true);
    }

    private static int CanUpdate(string installedSDK, string fetchedSDK)
    {
        int[] installedSDK_ = Array.ConvertAll(installedSDK.Split('.'), int.Parse);
        int[] fetchedSDK_ = Array.ConvertAll(fetchedSDK.Split('.'), int.Parse);
        int compareLength = Mathf.Min(installedSDK_.Length, fetchedSDK_.Length);
        for (int i = 0; i < compareLength; i++)
        {
            if (installedSDK_[i] > fetchedSDK_[i])
            {
                return -1;
            }
            else if (installedSDK_[i] < fetchedSDK_[i])
            {
                return 1;
            }
        }
        return 0;
    }

    private static void ErrorMessage(string msg, Exception e = null)
    {
        if (e != null) Debug.LogError(e.Message + "\n\n");
        else Debug.LogError(msg + "\n\n");
    }

    public static void abroProgress()
    {
        EditorUtility.DisplayProgressBar("Hotswap - Decompressing VRCA", "Decompressing Selected Avatar", abro.progress);
    }

    public static void HS2()
    {
        abro = AssetBundle.RecompressAssetBundleAsync(ProjTempPath + "/custom.vrca", ProjTempPath + "/uncompD.vrca", BuildCompression.Uncompressed);
        abro.completed += (AsyncOperation ao) =>
        {
            if (abro.success)
            {
                HS3();
            }
            else
            {
                Debug.LogError("Failed to decompress the dummy VRCA file.\n" + abro.result + "\n");
            }
        };
    }

    public static void HS3()
    {
        string AvatarIDpattern = @"avtr_[\w]{8}-[\w]{4}-[\w]{4}-[\w]{4}-[\w]{12}";
        Regex AvatarIDrgx = new Regex(AvatarIDpattern);
        string CABpattern = @"CAB-[\w]{32}";
        Regex CABrgx = new Regex(CABpattern);

        EditorUtility.DisplayProgressBar("Hotswap - Analazing VRCA", "Loading Dummy and Selected Avatar", 0.0f);

        string dummy = File.ReadAllText(ProjTempPath + "/uncompD.vrca");
        string avi = File.ReadAllText(ProjTempPath + "/uncomp.vrca");

        EditorUtility.DisplayProgressBar("Hotswap - Analazing VRCA", "Looking for Avatar IDs and CABs", 0.2f);

        Match m = AvatarIDrgx.Match(dummy);
        if (!m.Success)
        {
            Debug.LogError("Unable to find New Avatar ID in Dummy Avatar.\n");
            EditorUtility.ClearProgressBar();
            return;
        }
        string NewAvatarID = m.Value;

        EditorUtility.DisplayProgressBar("Hotswap - Analazing VRCA", "Looking for Avatar IDs and CABs", 0.4f);
        m = CABrgx.Match(dummy);
        if (!m.Success)
        {
            Debug.LogError("Unable to find New CAB in Dummy Avatar.\n");
            EditorUtility.ClearProgressBar();
            return;
        }
        string NewCAB = m.Value;

        EditorUtility.DisplayProgressBar("Hotswap - Analazing VRCA", "Looking for Avatar IDs and CABs", 0.6f);
        m = AvatarIDrgx.Match(avi);
        if (!m.Success)
        {
            Debug.LogError("Unable to find Old Avatar ID in selected Avatar.\n");
            EditorUtility.ClearProgressBar();
            return;
        }
        string OldAvatarID = m.Value;
        var OldAvatarIDmatches = Regex.Matches(avi, OldAvatarID);
        int OldIDn = OldAvatarIDmatches.Count;

        EditorUtility.DisplayProgressBar("Hotswap - Analazing VRCA", "Looking for Avatar IDs and CABs", 0.8f);
        m = CABrgx.Match(avi);
        if (!m.Success)
        {
            Debug.LogError("Unable to find Old CAB in selected Avatar.\n");
            EditorUtility.ClearProgressBar();
            return;
        }
        string OldCAB = m.Value;
        var OldCABmatches = Regex.Matches(avi, OldCAB);
        int OldCABn = OldCABmatches.Count;

        string GoodUnityVer = Application.unityVersion;
        List<string> wrongunityvers = new List<string>();
        List<int> wrongunityversN = new List<int>();
        string UnityVerFind = @"20[\d]{2}\.[\d]\.[\d]{2}f[\d]";
        MatchCollection UnityVerrgx = Regex.Matches(avi, UnityVerFind);
        foreach (Match match in UnityVerrgx)
        {
            string matched = match.Groups[0].Value;
            if (matched == GoodUnityVer) { continue; }
            if (wrongunityvers.Contains(matched))
            {
                int ind = wrongunityvers.IndexOf(matched);
                wrongunityversN[ind]++;
            }
            else
            {
                wrongunityvers.Add(matched);
                wrongunityversN.Add(1);
            }
        }

        avi = null; System.GC.Collect();
        EditorUtility.DisplayProgressBar("Hotswap - Analazing VRCA", "Loading Selected Avatar", 0.99f);
        byte[] avib = File.ReadAllBytes(ProjTempPath + "/uncomp.vrca");
        EditorUtility.ClearProgressBar();

        var changes = new List<(string, string, int)>();
        changes.Add((OldCAB, NewCAB, OldCABn)); changes.Add((OldAvatarID, NewAvatarID, OldIDn));
        if (wrongunityvers.Any())
        {
            for (int i = 0; i < wrongunityvers.Count; i++)
            {
                changes.Add((wrongunityvers[i], GoodUnityVer, wrongunityversN[i]));
            }
        }
        byte[] bytes = ComputeNewBytes(avib, changes);

        File.WriteAllBytes(ProjTempPath + "/uncomp2.vrca", bytes);
        EditorUtility.ClearProgressBar();
        bytes = null; System.GC.Collect();

        EditorUtility.DisplayProgressBar("Hotswap - Compressing VRCA", "Compressing Selected Avatar", 0.0f);
        compress();
    }
    public static void compress()
    {
        ProcessStartInfo startInfo = new ProcessStartInfo();
        startInfo.FileName = Application.dataPath + "/VRC Hotswap/Resources/VRC Hotswap Compressor.exe";
        startInfo.Arguments = " c \"" + ProjTempPath + "/uncomp2.vrca\" \"" + ProjTempPath + "/custom2.vrca\" no-console";
        startInfo.UseShellExecute = false;
        startInfo.WindowStyle = ProcessWindowStyle.Hidden;
        startInfo.RedirectStandardOutput = true;
        startInfo.CreateNoWindow = true;

        Process process = new Process();
        process.StartInfo = startInfo;
        process.EnableRaisingEvents = true;
        process.OutputDataReceived += new DataReceivedEventHandler((sender, e) =>
        {
            string data = e.Data;
            if (!String.IsNullOrEmpty(data) && float.TryParse(data, out float flo))
            {
                flo /= 100;
                syncContext.Post(_ => { EditorUtility.DisplayProgressBar("Hotswap - Compressing VRCA", "Compressing Selected Avatar", flo); }, null);
            }
        });
        process.Exited += new EventHandler((object s, System.EventArgs e) => { syncContext.Post(_ => { HS4(); }, null); });
        try
        {
            process.Start();
            process.BeginOutputReadLine();
        }
        catch (Exception e)
        {
            Debug.LogError(e.Message + "\n");
            throw;
        }
    }
    public static void HS4()
    {
        EditorUtility.ClearProgressBar();

        if (File.Exists(ProjTempPath + "/uncomp.vrca")) File.Delete(ProjTempPath + "/uncomp.vrca");
        if (File.Exists(ProjTempPath + "/uncomp2.vrca")) File.Delete(ProjTempPath + "/uncomp2.vrca");
        if (File.Exists(ProjTempPath + "/uncompD.vrca")) File.Delete(ProjTempPath + "/uncompD.vrca");

        if (File.Exists(ProjTempPath + "/custom2.vrca"))
        {
            if (File.Exists(ProjTempPath + "/custom.vrca")) File.Delete(ProjTempPath + "/custom.vrca");
            File.Move(ProjTempPath + "/custom2.vrca", ProjTempPath + "/custom.vrca");
            Debug.Log($"<color=cyan>HOTSWAP SUCCESSFUL</color>\n");
        }
        else
        {
            Debug.LogError($"Hotswap failed\n");
        }
        System.GC.Collect();
    }
    public static byte[] ComputeNewBytes(byte[] input, List<(string, string, int)> StringsToReplace)
    {   // mmmh yeah my brain hurts
        int StringsToReplaceCount = StringsToReplace.Count;

        byte[][] OldStrings = new byte[StringsToReplaceCount][];
        byte[][] NewStrings = new byte[StringsToReplaceCount][];
        int[] OldStringsCount = new int[StringsToReplaceCount];
        int[] OldStringsLength = new int[StringsToReplaceCount];
        ulong[] NewStringsLength = new ulong[StringsToReplaceCount];
        int[] StringsDeltaLenght = new int[StringsToReplaceCount];

        ulong inputL = (ulong)input.Length;
        ulong N = inputL;

        for (int i = 0; i < StringsToReplaceCount; i++)
        {
            var oldstr = System.Text.Encoding.UTF8.GetBytes(StringsToReplace[i].Item1);
            OldStrings[i] = oldstr;
            OldStringsLength[i] = oldstr.Length;
            int reps = StringsToReplace[i].Item3;
            OldStringsCount[i] = reps;
            var newstr = System.Text.Encoding.UTF8.GetBytes(StringsToReplace[i].Item2);
            NewStrings[i] = newstr;
            NewStringsLength[i] = (ulong)newstr.Length;
            int delta = newstr.Length - oldstr.Length;
            StringsDeltaLenght[i] = delta;
            N += (ulong)(delta * reps);
        }

        byte[] output = new byte[N];

        int[] OldStringsHits = new int[StringsToReplaceCount];

        float progress = 0.025f;
        EditorUtility.DisplayProgressBar("Hotswap - Generating new VRCA", "Combining data into your new Avatar", progress);

        for ((ulong input_index, ulong output_index) = (0, 0); input_index < inputL; input_index++, output_index++)
        {
            float prog = (float)input_index / inputL;
            if (prog > progress + 0.05f)
            {
                progress = prog;
                EditorUtility.DisplayProgressBar("Hotswap - Generating new VRCA", "Combining data into your new Avatar", progress);
            }

            if (output_index < N) output[output_index] = input[input_index];

            for (int i = 0; i < StringsToReplaceCount; i++)
            {
                if (OldStringsCount[i] > 0)
                {
                    if (input[input_index] == OldStrings[i][OldStringsHits[i]])
                    {
                        OldStringsHits[i]++;
                        if (OldStringsHits[i] == OldStringsLength[i])
                        {
                            output_index += NewStringsLength[i] - (ulong)OldStringsLength[i];
                            ulong L = NewStringsLength[i];
                            for (ulong j = 0; j < L; j++) output[output_index - j] = NewStrings[i][L - 1 - j];
                            OldStringsCount[i]--;
                            for (int j = 0; j < OldStringsHits.Length; j++) OldStringsHits[j] = 0;
                            break;
                        }
                    }
                    else OldStringsHits[i] = 0;
                }
            }
        }

        return output;
    }

    [MenuItem("VRC Hotswap/Spawn Dummy Avi", true)]
    static bool ValidateSpawnDummy()
    {
        return Validate_HasVRCSDK();
    }

    [MenuItem("VRC Hotswap/Spawn Dummy Avi")]
    public static void SpawnDummy()
    {
        var DummyAvi = GameObject.Find("Dummy Avi");
        if (!DummyAvi)
        {
            DummyAvi = UnityEngine.Object.Instantiate(Resources.Load("Dummy Avi Prefab") as GameObject);
            DummyAvi.name = "Dummy Avi";
            Debug.Log($"<color=cyan>Dummy Avatar Spawned Successfully!</color>\n");
        }
        else Debug.Log($"<color=cyan>Dummy Avatar already in Scene!</color>\n");

        Selection.activeGameObject = DummyAvi;

        Material mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/VRC Hotswap/Resources/DummyMat.mat");
        if (mat.shader.name == "VRChat/Mobile/Toon Lit") return;
        Shader vrcshader = Shader.Find("VRChat/Mobile/Toon Lit");
        if (vrcshader) mat.shader = vrcshader;
        else
        {
            Shader a_shader = Shader.Find("Unlit/Texture");
            if (a_shader) mat.shader = a_shader;
        }
    }
}
#endif
