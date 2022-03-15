#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
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
    private static readonly string custom_vrca_path = ProjTempPath + "/custom.vrca";
    private static readonly string decomp_vrca_path = ProjTempPath + "/decomp.vrca";
    private static readonly string decompMod_vrca_path = ProjTempPath + "/decomp2.vrca";
    private static readonly string decompDummy_vrca_path = ProjTempPath + "/decompD.vrca";
    private static readonly string customTmp_vrca_path = ProjTempPath + "/custom2.vrca";
    private static readonly string customBKP_vrca_path = ProjTempPath + "/customBKP.vrca";
    private static readonly Encoding decomp_vrca_enc = Encoding.GetEncoding(28591);
    private static AssetBundleRecompressOperation abro;
    private static AssetBundleCreateRequest abcr;

    [MenuItem("VRC Hotswap/Hotswap", true)]
    static bool ValidateHotswap()
    {
        if (!Application.isPlaying)
        {
            File.Delete(custom_vrca_path);
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
                    if (File.Exists(custom_vrca_path))
                    {
                        return true;
                    }
                }
            }
        }
        return false;
    }
    [InitializeOnLoadMethod]
    public static void InitBKPCleaner()
    {
        EditorApplication.playModeStateChanged += ExitingPlayMode;
        if (!EditorApplication.isPlaying)
        {
            File.Delete(customBKP_vrca_path);
        }
    }
    private static void ExitingPlayMode(PlayModeStateChange state)
    {
        if (state.ToString() == "ExitingPlayMode")
        {
            File.Delete(customBKP_vrca_path);
        }
    }

    [MenuItem("VRC Hotswap/Hotswap")]
    public static void Hotswap()
    {
        if (File.Exists(customBKP_vrca_path))
        {
            File.Copy(customBKP_vrca_path, custom_vrca_path, true);
        }
        else
        {
            File.Copy(custom_vrca_path, customBKP_vrca_path, true);
        }
        
        File.Delete(decomp_vrca_path);
        File.Delete(decompMod_vrca_path);
        File.Delete(decompDummy_vrca_path);
        File.Delete(customTmp_vrca_path);

        if (EditorUtility.DisplayDialog("VRC Hotswap", "Please select the avatar file you want to hotswap", "Continue", "Cancel"))
        {
            string vrcapath = EditorUtility.OpenFilePanelWithFilters("Select VRCA File for Hotswap", "", new string[] { "Avatar Files", "vrca", "All files", "*" });

            if (string.IsNullOrEmpty(vrcapath))
            {
                Debug.LogWarning("Hotwap cancelled.\n");
                EditorApplication.Beep();
                return;
            }
            Debug.Log("Selected file for Hotwap:\n" + vrcapath + "\n");

            abro = AssetBundle.RecompressAssetBundleAsync(vrcapath, decomp_vrca_path, BuildCompression.Uncompressed);
            EditorUtility.DisplayProgressBar("Hotswap - Decompressing VRCA", "Decompressing Selected Avatar", 0.0f);
            EditorApplication.update += abroProgress;
            abro.completed += (AsyncOperation ao) =>
            {
                EditorApplication.update -= abroProgress;
                EditorUtility.ClearProgressBar();
                if (abro.success)
                {
                    abro = null;
                    HS2();
                }
                else
                {
                    Debug.LogError("Failed to decompress the Selected Avatar file.\n" + abro.result + "\n");
                    abro = null;
                    EditorApplication.Beep();
                }
            };
        }
        else
        {
            Debug.LogWarning("Hotwap cancelled.\n");
            EditorApplication.Beep();
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
        File.Delete(CustomPrevImagesFilepath);
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
        EditorApplication.Beep();
    }

    public static void abroProgress()
    {
        EditorUtility.DisplayProgressBar("Hotswap - Decompressing VRCA", "Decompressing Selected Avatar", abro.progress);
    }
    public static void abcrProgress()
    {
        EditorUtility.DisplayProgressBar("Hotswap - Checking Hotswapped VRCA", "Making sure the New Avatar can be loaded into Unity", abcr.progress);
    }

    public static void HS2()
    {
        abro = AssetBundle.RecompressAssetBundleAsync(custom_vrca_path, decompDummy_vrca_path, BuildCompression.Uncompressed);
        EditorUtility.DisplayProgressBar("Hotswap - Decompressing VRCA", "Decompressing Selected Avatar", 0.5f);
        abro.completed += (AsyncOperation ao) =>
        {
            EditorUtility.ClearProgressBar();
            if (abro.success)
            {
                abro = null;
                HS3();
            }
            else
            {
                Debug.LogError("Failed to decompress the Dummy Avatar file.\n" + abro.result + "\n");
                abro = null;
                EditorApplication.Beep();
            }
        };
    }
    public static void Regex_Lists(string line, string pattern, List<string> matches, string exclusion = null)
    {
        MatchCollection mc = Regex.Matches(line, pattern);
        foreach (Match match in mc)
        {
            string matched = match.Groups[0].Value;
            if (exclusion != null && matched == exclusion) { continue; }
            var matchindex = matches.IndexOf(matched);
            if (matchindex == -1)
            {
                matches.Add(matched);
            }
        }
    }

    public static void HS3()
    {
        string PrefabPattern = @"prefab-id-v1_avtr_[\w]{8}-[\w]{4}-[\w]{4}-[\w]{4}-[\w]{12}_[\d]{10}\.prefab";
        Regex PrefabPatternRgx = new Regex(PrefabPattern);
        string CABpattern = @"CAB-[\w]{32}";
        Regex CABrgx = new Regex(CABpattern);
        string AvatarIDpattern = @"avtr_[\w]{8}-[\w]{4}-[\w]{4}-[\w]{4}-[\w]{12}";
        Regex AvatarIDrgx = new Regex(AvatarIDpattern);
        string UnityVerPattern = @"20[\d]{2}\.[\d]\.[\d]{2}f[\d]";

        string OldCAB = null;
        string OldPrefab = null;
        string OldAvatarID = null;
        string GoodUnityVer = Application.unityVersion;
        List<string> wrongunityvers = new List<string>();
        bool olderAviFlag = false;
        bool olderUnityVer = false;

        using (StreamReaderOver reader = new StreamReaderOver(decomp_vrca_path, decomp_vrca_enc))
        {
            EditorUtility.DisplayProgressBar("Hotswap - Analazing VRCA", "Scanning selected Avatar", 0);
            float FileLength_prog = (float)reader.BaseStream.Length;
            float progress = 0;
            string line;
            MatchCollection mc;

            while (!reader.EndOfStream)
            {
                float prog = reader.BaseStream.Position / FileLength_prog;
                if (prog > progress + 0.005)
                {
                    progress = prog;
                    EditorUtility.DisplayProgressBar("Hotswap - Analazing VRCA", "Scanning selected Avatar", progress);
                }

                line = reader.ReadLine();

                mc = Regex.Matches(line, CABpattern);
                if (mc.Count > 0) OldCAB = mc[mc.Count - 1].Value;

                if (OldPrefab == null)
                {
                    if (OldAvatarID == null)
                    {
                        Match mm = AvatarIDrgx.Match(line);
                        if (mm.Success)
                        {
                            OldAvatarID = mm.Value;
                        }
                    }
                    mc = Regex.Matches(line, PrefabPattern);
                    if (mc.Count > 0)
                    {
                        OldPrefab = mc[0].Value;
                        string temp = OldPrefab.Replace("prefab-id-v1_", "");
                        OldAvatarID = temp.Substring(0, temp.Length - 18);
                    }
                    if (!olderAviFlag)
                    {
                        olderAviFlag = line.Contains("customavatar.unity3d");
                        if (olderAviFlag) Debug.Log("The Selected Avatar seems to be SDK2...\n");
                    }
                }

                if (!olderUnityVer)
                {
                    olderUnityVer = line.Contains("5.6.3f1");
                }
                Regex_Lists(line, UnityVerPattern, wrongunityvers, GoodUnityVer);
            }
        }
        if (OldCAB == null)
        {
            Debug.LogError("Unable to find Old CAB in Selected Avatar.\n");
            EditorUtility.ClearProgressBar();
            EditorApplication.Beep();
            return;
        }
        if (OldAvatarID == null)
        {
            Debug.LogError("Unable to find Old Avatar ID in Selected Avatar.\n");
            EditorUtility.ClearProgressBar();
            EditorApplication.Beep();
            return;
        }
        if (!olderAviFlag && OldPrefab == null)
        {
            Debug.LogError("Unable to find Old Prefab in Selected Avatar.\n");
            EditorUtility.ClearProgressBar();
            EditorApplication.Beep();
            return;
        }
        if (olderUnityVer) Debug.LogWarning("The Selected Avatar was created on Unity version 5.6.3f1, which can't be 100% disguised during Hotswap.\n");

        EditorUtility.DisplayProgressBar("Hotswap - Analazing VRCA", "Scanning Dummy Avatar", 0.5f);
        string dummy = File.ReadAllText(decompDummy_vrca_path);

        EditorUtility.DisplayProgressBar("Hotswap - Analazing VRCA", "Scanning Dummy Avatar", 0.99f);
        Match m = PrefabPatternRgx.Match(dummy);
        if (!m.Success)
        {
            Debug.LogError("Unable to find New Avatar ID in Dummy Avatar.\n");
            EditorUtility.ClearProgressBar();
            EditorApplication.Beep();
            return;
        }
        string NewPrefab = m.Value;
        string tempaviid = NewPrefab.Replace("prefab-id-v1_", "");
        string NewAvatarID = tempaviid.Substring(0, tempaviid.Length - 18);

        m = CABrgx.Match(dummy);
        if (!m.Success)
        {
            Debug.LogError("Unable to find New CAB in Dummy Avatar.\n");
            EditorUtility.ClearProgressBar();
            EditorApplication.Beep();
            return;
        }
        string NewCAB = m.Value;
        dummy = null;

        EditorUtility.ClearProgressBar();

        var changes = new List<(string, string)>();
        if (olderAviFlag)
        {   //sad offset
            //changes.Add(("_customavatar.prefab", NewPrefab));
            //changes.Add(("customavatar.unity3d", NewPrefab+ ".unity3d"));
            //changes.Add(("_CustomAvatar", NewPrefab.Replace(".prefab", "")));
            //changes.Add(("5.6.3f1", GoodUnityVer));
        }
        else { changes.Add((OldPrefab, NewPrefab)); }
        changes.Add((OldCAB, NewCAB)); changes.Add((OldAvatarID, NewAvatarID));
        if (wrongunityvers.Any())
        {
            for (int i = 0; i < wrongunityvers.Count; i++)
            {
                changes.Add((wrongunityvers[i], GoodUnityVer));
            }
        }

        if (olderUnityVer)
        {
            EditorApplication.Beep();
            if (!EditorUtility.DisplayDialog("Hotswap", "The selected avatar was created on Unity version 5.6.3f1, " +
                "which can't be 100% disguised during Hotswap.\n\nProceed at your own risk.", "Continue", "Cancel"))
            {
                File.Delete(decomp_vrca_path);
                File.Delete(decompMod_vrca_path);
                File.Delete(decompDummy_vrca_path);
                Debug.LogWarning("Hotwap aborted.\n");
                return;
            }
        }

        CreateModifiedFile(decomp_vrca_path, decompMod_vrca_path, changes);

        EditorUtility.DisplayProgressBar("Hotswap - Compressing VRCA", "Compressing Selected Avatar", 0.0f);
        compress();
    }
    public static void compress()
    {
        ProcessStartInfo startInfo = new ProcessStartInfo();
        startInfo.FileName = Application.dataPath + "/VRC Hotswap/Resources/VRC Hotswap Compressor.exe";
        startInfo.Arguments = " c \"" + decompMod_vrca_path + "\" \"" + customTmp_vrca_path + "\" no-console";
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

        File.Delete(decomp_vrca_path);
        File.Delete(decompMod_vrca_path);
        File.Delete(decompDummy_vrca_path);

        if (File.Exists(customTmp_vrca_path))
        {
            abcr = AssetBundle.LoadFromFileAsync(customTmp_vrca_path);
            EditorUtility.DisplayProgressBar("Hotswap - Checking Hotswapped VRCA", "Making sure the New Avatar can be loaded into Unity", 0.0f);
            EditorApplication.update += abcrProgress;
            abcr.completed += (AsyncOperation ao) =>
            {
                EditorApplication.update -= abcrProgress;
                EditorUtility.ClearProgressBar();
                var assetbundle = abcr.assetBundle;
                if (!assetbundle)
                {
                    abcr = null;
                    Debug.LogError($"Hotswap failed. New Avatar file ended up malformed.\n");
                    EditorApplication.Beep();
                }
                else
                {
                    bool hasprefab = false;
                    foreach (string asset in assetbundle.GetAllAssetNames())
                    {
                        if (asset.EndsWith(".prefab"))
                        {
                            hasprefab = true; break;
                        }
                    }
                    assetbundle.Unload(true);
                    abcr = null;
                    if (!hasprefab)
                    {
                        Debug.LogError($"Hotswap failed. New Avatar wouldn't load in Unity.\n");
                        EditorApplication.Beep();
                    } else HS5();
                }
            };
        }
        else
        {
            Debug.LogError($"Hotswap failed. Recompression of New Avatar failed.\n");
            EditorApplication.Beep();
        }
    }
    public static void HS5()
    {
        File.Delete(custom_vrca_path);
        File.Move(customTmp_vrca_path, custom_vrca_path);
        EditorApplication.Beep();
        if (EditorUtility.DisplayDialog("VRC Hotswap", "HOTSWAP SUCCESSFUL!\n\n", "Ok", "Nice"))
        {
            Debug.Log($"<color=cyan>HOTSWAP SUCCESSFUL</color>\n");
        } else Debug.Log($"<color=cyan>HOTSWAP SUCCESSFUL</color>\n<color=green>Nice.</color>");
    }
    public static void CreateModifiedFile(string input_file, string output_file, List<(string, string)> StringsToReplace)
    {
        using (StreamReaderOver reader = new StreamReaderOver(input_file, decomp_vrca_enc))
        {
            EditorUtility.DisplayProgressBar("Hotswap - Creating Modified VRCA", "Replacing values into new Avatar", 0);
            float FileLength_prog = (float)reader.BaseStream.Length;
            float progress = 0;
            using (StreamWriter writer = new StreamWriter(output_file, false, decomp_vrca_enc))
            {
                while (!reader.EndOfStream)
                {
                    float prog = reader.BaseStream.Position / FileLength_prog;
                    if (prog > progress + 0.005)
                    {
                        progress = prog;
                        EditorUtility.DisplayProgressBar("Hotswap - Creating Modified VRCA", "Replacing values into new Avatar", progress);
                    }

                    string line = reader.ReadLine();

                    foreach (var replace in StringsToReplace)
                    {
                        line = line.Replace(replace.Item1, replace.Item2);
                    }
                    writer.Write(line);
                }
            }
        }
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

class StreamReaderOver : StreamReader // thanks to ShrekamusChrist
{
    public StreamReaderOver(Stream stream) : base(stream) { }

    public StreamReaderOver(string path, Encoding encoding) : base(path, encoding) { }

    public override string ReadLine()
    {
        StringBuilder sb = new StringBuilder();
        while (true)
        {
            int ch = Read();
            switch (ch)
            {
                case -1:
                    goto exitloop;
                case 10: // \n
                    sb.Append('\n');
                    goto exitloop;
                case 13: // \r
                    sb.Append('\r');
                    goto exitloop;
                default:
                    sb.Append((char)ch);
                    break;
            }
        }
        exitloop:
        return sb.Length > 0 ? sb.ToString() : null;
    }
}
#endif
