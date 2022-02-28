#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

public class VRCHotswap
{
    public static SynchronizationContext syncContext = SynchronizationContext.Current;
    private static string Temp = Application.temporaryCachePath;
    private static AssetBundleRecompressOperation abro;

    [MenuItem("VRC Hotswap/Hotswap", true)]
    static bool ValidateHotswap()
    {
        if (!Application.isPlaying)
        {
            if (File.Exists(Temp + "/custom.vrca"))
            {
                File.Delete(Temp + "/custom.vrca");
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
                    if (File.Exists(Temp + "/custom.vrca"))
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
        if (File.Exists(Temp + "/uncomp.vrca")) File.Delete(Temp + "/uncomp.vrca");
        if (File.Exists(Temp + "/uncomp2.vrca")) File.Delete(Temp + "/uncomp2.vrca");
        if (File.Exists(Temp + "/uncompD.vrca")) File.Delete(Temp + "/uncompD.vrca");
        if (File.Exists(Temp + "/custom2.vrca")) File.Delete(Temp + "/custom2.vrca");

        if (EditorUtility.DisplayDialog("Hotswap", "Please select the avatar file you want to hotswap", "Continue", "Cancel"))
        {
            string vrcapath = EditorUtility.OpenFilePanelWithFilters("Select VRCA File for Hotswap", "", new string[] { "Avatar Files", "vrca", "All files", "*" });

            if (string.IsNullOrEmpty(vrcapath))
            {
                Debug.LogWarning("Hotwap cancelled.");
                return;
            }
            Debug.Log("Selected file for Hotwap:\n" + vrcapath);

            abro = AssetBundle.RecompressAssetBundleAsync(vrcapath, Temp + "/uncomp.vrca", BuildCompression.Uncompressed);
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
                    Debug.LogError("Failed to decompress the selected VRCA file.\n" + abro.result);
                }
            };
        }
        else
        {
            Debug.LogWarning("Hotwap cancelled.");
        }
    }

    public static void abroProgress()
    {
        EditorUtility.DisplayProgressBar("Hotswap - Decompressing VRCA", "Decompressing Selected Avatar", abro.progress);
    }

    public static void HS2()
    {
        abro = AssetBundle.RecompressAssetBundleAsync(Temp + "/custom.vrca", Temp + "/uncompD.vrca", BuildCompression.Uncompressed);
        abro.completed += (AsyncOperation ao) =>
        {
            if (abro.success)
            {
                HS3();
            }
            else
            {
                Debug.LogError("Failed to decompress the dummy VRCA file.\n" + abro.result);
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

        string dummy = File.ReadAllText(Temp + "/uncompD.vrca");
        string avi = File.ReadAllText(Temp + "/uncomp.vrca");

        EditorUtility.DisplayProgressBar("Hotswap - Analazing VRCA", "Looking for Avatar IDs and CABs", 0.2f);
        Match m = AvatarIDrgx.Match(dummy);
        if (!m.Success)
        {
            Debug.LogError("Unable to find New Avatar ID in Dummy Avatar");
            EditorUtility.ClearProgressBar();
            return;
        }
        string NewAvatarID = m.Value;

        EditorUtility.DisplayProgressBar("Hotswap - Analazing VRCA", "Looking for Avatar IDs and CABs", 0.4f);
        m = CABrgx.Match(dummy);
        if (!m.Success)
        {
            Debug.LogError("Unable to find New CAB in Dummy Avatar");
            EditorUtility.ClearProgressBar();
            return;
        }
        string NewCAB = m.Value;

        EditorUtility.DisplayProgressBar("Hotswap - Analazing VRCA", "Looking for Avatar IDs and CABs", 0.6f);
        m = AvatarIDrgx.Match(avi);
        if (!m.Success)
        {
            Debug.LogError("Unable to find Old Avatar ID in selected Avatar");
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
            Debug.LogError("Unable to find Old CAB in selected Avatar");
            EditorUtility.ClearProgressBar();
            return;
        }
        string OldCAB = m.Value;
        var OldCABmatches = Regex.Matches(avi, OldCAB);
        int OldCABn = OldCABmatches.Count;

        avi = null;
        EditorUtility.DisplayProgressBar("Hotswap - Analazing VRCA", "Loading Selected Avatar", 0.99f);
        byte[] avib = File.ReadAllBytes(Temp + "/uncomp.vrca");
        EditorUtility.ClearProgressBar();
        
        byte[] bytes = newbytes(avib, System.Text.Encoding.UTF8.GetBytes(OldCAB), System.Text.Encoding.UTF8.GetBytes(NewCAB),
            System.Text.Encoding.UTF8.GetBytes(OldAvatarID), System.Text.Encoding.UTF8.GetBytes(NewAvatarID), OldCABn, OldIDn);
        File.WriteAllBytes(Temp + "/uncomp2.vrca", bytes);
        //File.Delete(Temp + "/custom.vrca");
        EditorUtility.ClearProgressBar();
        bytes = null;

        EditorUtility.DisplayProgressBar("Hotswap - Compressing VRCA", "Compressing Selected Avatar", 0.0f);
        compress();
    }
    public static void compress()
    {
        ProcessStartInfo startInfo = new ProcessStartInfo();
        startInfo.FileName = Application.dataPath + "/VRC Hotswap/Resources/VRC Hotswap Compressor.exe";
        startInfo.Arguments = " c \"" + Temp + "/uncomp2.vrca\" \"" + Temp + "/custom2.vrca\" no-console";
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
            Debug.LogError(e);
            throw;
        }
    }
    public static void HS4()
    {
        EditorUtility.ClearProgressBar();

        if (File.Exists(Temp + "/uncomp.vrca")) File.Delete(Temp + "/uncomp.vrca");
        if (File.Exists(Temp + "/uncomp2.vrca")) File.Delete(Temp + "/uncomp2.vrca");
        if (File.Exists(Temp + "/uncompD.vrca")) File.Delete(Temp + "/uncompD.vrca");

        if (File.Exists(Temp + "/custom2.vrca"))
        {
            if (File.Exists(Temp + "/custom.vrca")) File.Delete(Temp + "/custom.vrca");
            File.Move(Temp + "/custom2.vrca", Temp + "/custom.vrca");
            Debug.Log($"<color=cyan>HOTSWAP SUCCESSFUL</color>");
        }
        else
        {
            Debug.LogError($"Hotswap failed\n");
        }
    }
    public static byte[] newbytes(byte[] input, byte[] oldCAB, byte[] newCAB, byte[] oldID, byte[] newID, int nCAB, int nID)
    {   // mmmh yeah my brain hurts
        ulong inputL = (ulong)input.Length;
        ulong newCABL = (ulong)newCAB.Length;
        ulong newIDL = (ulong)newID.Length;
        int oldCABL = oldCAB.Length;
        int oldIDL = oldID.Length;
        int deltaCAB = (int)newCABL - oldCABL;
        int deltaID = (int)newIDL - oldIDL;
        ulong N = inputL + (ulong)(nCAB * deltaCAB + nID * deltaID);
        byte[] output = new byte[N];

        ulong index = 0;
        ulong indexold = 0;
        int CABhit = 0;
        int IDhit = 0;

        float progress = 0.05f;
        EditorUtility.DisplayProgressBar("Hotswap - Generating new VRCA", "Combining data into your new Avatar", progress);
        
        while (indexold < inputL)
        {
            float prog = (float)indexold / inputL;
            if (prog > progress + 0.1f)
            {
                progress = prog;
                EditorUtility.DisplayProgressBar("Hotswap - Generating new VRCA", "Combining data into your new Avatar", progress);
            }
            
            if (index < N)
            {
                output[index] = input[indexold];
            }
            if (nCAB > 0)
            {
                if (input[indexold] == oldCAB[CABhit])
                {
                    CABhit++;
                    if (CABhit == oldCABL)
                    {
                        index = index + (ulong)deltaCAB;
                        for (ulong j = 0; j < newCABL; j++)
                        {
                            output[index - j] = newCAB[newCABL - 1 - j];
                        }
                        nCAB--; IDhit = CABhit = 0;
                        index++; indexold++;
                        continue;
                    }
                }
                else CABhit = 0;
            }
            if (nID > 0)
            {
                if (input[indexold] == oldID[IDhit])
                {
                    IDhit++;
                    if (IDhit == oldIDL)
                    {
                        index = index + (ulong)deltaID;
                        for (ulong j = 0; j < newIDL; j++)
                        {
                            output[index - j] = newID[newIDL - 1 - j];
                        }
                        nID--; CABhit = IDhit = 0;
                        index++; indexold++;
                        continue;
                    }
                }
                else IDhit = 0;
            }

            index++; indexold++;
        }

        return output;
    }

    [MenuItem("VRC Hotswap/Spawn Dummy Avi")]
    public static void SpawnDummy()
    {
        var DummyAvi = GameObject.Find("Dummy Avi");
        if (!DummyAvi)
        {
            DummyAvi = UnityEngine.Object.Instantiate(Resources.Load("Dummy Avi Prefab") as GameObject);
            DummyAvi.name = "Dummy Avi";
        }
        Debug.Log("Dummy Avatar Spawned Successfully!");

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
