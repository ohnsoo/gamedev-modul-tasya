#if UNITY_EDITOR
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

/// <summary>
/// Membangun kota baru Antar Cepat dari data tata letak (jalan, trotoar, marka, bangunan, pohon, lampu, lalu lintas, warga, langit).
/// Bisa dijalankan ulang: objek "Kota Baru" lama dihapus dan dibangun lagi.
/// </summary>
public static class KotaBuilder
{
    const string ROOT = "Assets/AntarCepat/Kota/";
    static readonly CultureInfo CI = CultureInfo.InvariantCulture;
    static float F(string s) => float.Parse(s, CI);
    static int I(string s) => int.Parse(s, CI);

    static readonly Dictionary<string, GameObject> cachePrefab = new Dictionary<string, GameObject>();
    static GameObject Prefab(string nama)
    {
        if (cachePrefab.TryGetValue(nama, out var g)) return g;
        GameObject hasil = null;
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab " + nama))
        {
            var p = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileNameWithoutExtension(p).Trim() != nama.Trim()) continue;
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(p);
            if (hasil == null || p.Contains("SimpleTown")) hasil = go;
        }
        cachePrefab[nama] = hasil;
        return hasil;
    }

    static Mesh MuatMesh(string file, string nama)
    {
        var baris = File.ReadAllLines(file);
        var kepala = baris[0].Split(' ');
        int nv = I(kepala[0]), nt = I(kepala[1]);
        var v = new Vector3[nv]; var uv = new Vector2[nv];
        for (int i = 0; i < nv; i++)
        {
            var s = baris[1 + i].Split(' ');
            v[i] = new Vector3(F(s[0]), F(s[1]), F(s[2])); uv[i] = new Vector2(F(s[3]), F(s[4]));
        }
        var tri = new int[nt * 3];
        for (int i = 0; i < nt; i++)
        {
            var s = baris[1 + nv + i].Split(' ');
            tri[i * 3] = I(s[0]); tri[i * 3 + 1] = I(s[1]); tri[i * 3 + 2] = I(s[2]);
        }
        string path = ROOT + "Mesh/" + nama + ".asset";
        var m = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        bool baru = m == null;
        if (baru) m = new Mesh();
        m.Clear();
        m.name = nama;
        m.indexFormat = nv > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
        m.vertices = v; m.uv = uv; m.triangles = tri;
        m.RecalculateNormals(); m.RecalculateBounds(); m.RecalculateTangents();
        if (baru) { Directory.CreateDirectory(ROOT + "Mesh"); AssetDatabase.CreateAsset(m, path); }
        else EditorUtility.SetDirty(m);
        return m;
    }

    static Material Bahan(string nama, Color warna, string tekstur = null, float halus = 0.1f, bool unlit = false, Color? emisi = null, float metal = 0f)
    {
        string path = ROOT + "Material/" + nama + ".mat";
        var sh = Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit");
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { Directory.CreateDirectory(ROOT + "Material"); m = new Material(sh); AssetDatabase.CreateAsset(m, path); }
        m.shader = sh;
        m.SetColor("_BaseColor", warna);
        if (tekstur != null) m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(ROOT + "Tekstur/" + tekstur + ".png"));
        if (!unlit) { m.SetFloat("_Smoothness", halus); m.SetFloat("_Metallic", metal); }
        if (emisi.HasValue)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", emisi.Value);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        }
        else m.DisableKeyword("_EMISSION");
        EditorUtility.SetDirty(m);
        return m;
    }

    static GameObject Anak(Transform induk, string nama)
    {
        var g = new GameObject(nama); g.transform.SetParent(induk, false); return g;
    }

    static GameObject Permukaan(Transform induk, string nama, Mesh mesh, Material bahan, bool collider, bool bayangan)
    {
        var g = Anak(induk, nama);
        g.AddComponent<MeshFilter>().sharedMesh = mesh;
        var r = g.AddComponent<MeshRenderer>(); r.sharedMaterial = bahan;
        r.shadowCastingMode = bayangan ? ShadowCastingMode.On : ShadowCastingMode.Off;
        if (collider) g.AddComponent<MeshCollider>().sharedMesh = mesh;
        GameObjectUtility.SetStaticEditorFlags(g, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
        return g;
    }

    static GameObject Pasang(GameObject prefab, Transform induk, Vector3 pos, float rotY, float skala = 1f, bool statis = true)
    {
        var g = (GameObject)PrefabUtility.InstantiatePrefab(prefab, induk);
        g.transform.position = pos;
        g.transform.rotation = Quaternion.Euler(0f, rotY, 0f);
        g.transform.localScale = Vector3.one * skala;
        if (statis) GameObjectUtility.SetStaticEditorFlags(g, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
        return g;
    }

    public static string Bangun(string data, string versi)
    {
        var log = new System.Text.StringBuilder();
        var scene = EditorSceneManager.GetActiveScene();
        var lamaRoot = GameObject.Find("Kota Baru");
        if (lamaRoot != null) Object.DestroyImmediate(lamaRoot);

        // ---------- bahan ----------
        var mAspal = Bahan("Kota_Aspal", Color.white, "Kota_Aspal", 0.18f);
        var mTrotoar = Bahan("Kota_Trotoar", Color.white, "Kota_Trotoar", 0.12f);
        var mRumput = Bahan("Kota_Rumput", Color.white, "Kota_Rumput", 0.02f);
        var mMarka = Bahan("Kota_Marka", new Color(0.96f, 0.96f, 0.93f), null, 0.25f);
        var mBadan = Bahan("Lalin_Badan", new Color(0.09f, 0.1f, 0.11f), null, 0.35f);
        var mTiang = Bahan("Lalin_Tiang", new Color(0.36f, 0.38f, 0.41f), null, 0.45f, false, null, 0.4f);
        var mStrip = Bahan("Lalin_Strip", new Color(0.95f, 0.95f, 0.95f), null, 0.3f);
        var mMati = Bahan("Lalin_Mati", new Color(0.07f, 0.07f, 0.07f), null, 0.6f);
        var mMerah = Bahan("Lalin_MerahNyala", new Color(4.0f, 0.25f, 0.15f), null, 0f, true);
        var mKuning = Bahan("Lalin_KuningNyala", new Color(4.0f, 2.3f, 0.2f), null, 0f, true);
        var mHijau = Bahan("Lalin_HijauNyala", new Color(0.25f, 3.6f, 1.1f), null, 0f, true);
        var mAwan = Bahan("Kota_Awan", Color.white, null, 0f, false, new Color(0.32f, 0.34f, 0.38f));
        var mBukit = Bahan("Kota_Bukit", new Color(0.36f, 0.56f, 0.3f), null, 0f);
        var mBukitAtas = Bahan("Kota_BukitAtas", new Color(0.5f, 0.63f, 0.44f), null, 0f);

        // ---------- data ----------
        var nodes = new List<Vector3>(); var lit = new List<bool>(); var rad = new List<float>();
        var edges = new List<Vector2Int>();
        var zNode = new List<int>(); var zArm = new List<int>(); var zPos = new List<Vector3>(); var zAx = new List<Vector3>();
        var pedN = new List<Vector3>(); var pedE = new List<Vector3Int>();
        var baris = File.ReadAllLines(data + "layout_kota_" + versi + ".txt");

        var root = new GameObject("Kota Baru").transform;
        var tPerm = Anak(root, "Permukaan").transform;
        var tBang = Anak(root, "Bangunan").transform;
        var tPohon = Anak(root, "Pohon").transform;
        var tLampu = Anak(root, "Lampu Jalan").transform;
        var tLalin = Anak(root, "Lalu Lintas").transform;
        var tMobil = Anak(root, "Mobil").transform;
        var tWarga = Anak(root, "Warga").transform;
        var tLangit = Anak(root, "Langit").transform;

        // permukaan
        float tinggiJalan = 0.12f;
        Permukaan(tPerm, "Jalan", MuatMesh(data + "jalan_" + versi + ".txt", "Kota_Jalan"), mAspal, true, false);
        Permukaan(tPerm, "Trotoar", MuatMesh(data + "trotoar_" + versi + ".txt", "Kota_Trotoar"), mTrotoar, true, false);
        Permukaan(tPerm, "Marka", MuatMesh(data + "marka_" + versi + ".txt", "Kota_Marka"), mMarka, false, false);
        Permukaan(tPerm, "Rumput", MuatMesh(data + "rumput_" + versi + ".txt", "Kota_Rumput"), mRumput, true, false);
        var jauh = Permukaan(tPerm, "Rumput Jauh", MuatMesh(data + "rumput_jauh_" + versi + ".txt", "Kota_RumputJauh"), mRumput, false, false);

        var lalin = tLalin.gameObject.AddComponent<KotaJalan>();
        var lampuNode = new List<KotaLampu>();
        var sigData = new List<string[]>(); var pedData = new List<string[]>();
        var bangunan = 0; var pohon = 0; var lampu = 0;
        var mobilData = new List<string[]>(); var wargaData = new List<string[]>();

        foreach (var l in baris)
        {
            var s = l.Split(' ');
            switch (s[0])
            {
                case "N": nodes.Add(new Vector3(F(s[1]), tinggiJalan, F(s[2]))); lit.Add(s[3] == "1"); rad.Add(F(s[4])); break;
                case "E": edges.Add(new Vector2Int(I(s[1]), I(s[2]))); break;
                case "Z": zNode.Add(I(s[1])); zArm.Add(I(s[2])); zPos.Add(new Vector3(F(s[3]), tinggiJalan, F(s[4]))); zAx.Add(new Vector3(F(s[5]), 0f, F(s[6]))); break;
                case "S": sigData.Add(s); break;
                case "P": pedData.Add(s); break;
                case "Q": pedN.Add(new Vector3(F(s[1]), 0.27f, F(s[2]))); break;
                case "R": pedE.Add(new Vector3Int(I(s[1]), I(s[2]), I(s[3]))); break;
                case "B":
                {
                    var pf = Prefab(s[1]); if (pf == null) { log.AppendLine("prefab hilang " + s[1]); break; }
                    Pasang(pf, tBang, new Vector3(F(s[2]), 0f, F(s[3])), F(s[4])); bangunan++; break;
                }
                case "T":
                {
                    var pf = Prefab(s[1]); if (pf == null) { log.AppendLine("prefab hilang " + s[1]); break; }
                    Pasang(pf, tPohon, new Vector3(F(s[2]), 0f, F(s[3])), F(s[4]), F(s[5])); pohon++; break;
                }
                case "L":
                {
                    var pf = Prefab("lamp_mesh");
                    var g = Pasang(pf, tLampu, new Vector3(F(s[1]), 0.27f, F(s[2])), F(s[3]));
                    lampu++; break;
                }
                case "C": mobilData.Add(s); break;
                case "W": wargaData.Add(s); break;
            }
        }

        // lampu lalu lintas per node
        for (int n = 0; n < nodes.Count; n++)
        {
            if (!lit[n]) { lampuNode.Add(null); continue; }
            var g = Anak(tLalin, "Lampu Simpang " + n);
            g.transform.position = nodes[n];
            var kl = g.AddComponent<KotaLampu>();
            kl.offsetAwal = n * 7.3f; kl.hijauUS = 14f; kl.hijauTB = 14f;
            kl.bahanMati = mMati; kl.bahanMerah = mMerah; kl.bahanKuning = mKuning; kl.bahanHijau = mHijau;
            lampuNode.Add(kl);
        }
        var fbxLalin = AssetDatabase.LoadAssetAtPath<GameObject>(ROOT + "Model/Lampu_Lalin.fbx");
        var fbxPeny = AssetDatabase.LoadAssetAtPath<GameObject>(ROOT + "Model/Lampu_Penyeberang.fbx");
        var kumpul = new Dictionary<KotaLampu, Dictionary<string, List<Renderer>>>();
        System.Action<KotaLampu, string, Renderer> tambah = (kl, k, r) =>
        {
            if (!kumpul.TryGetValue(kl, out var d)) kumpul[kl] = d = new Dictionary<string, List<Renderer>>();
            if (!d.TryGetValue(k, out var lst)) d[k] = lst = new List<Renderer>();
            lst.Add(r);
        };
        System.Action<GameObject> bahanLampu = (g) =>
        {
            foreach (var r in g.GetComponentsInChildren<Renderer>())
            {
                string nm = r.name;
                if (nm.StartsWith("Merah") || nm.StartsWith("Kuning") || nm.StartsWith("Hijau")) r.sharedMaterial = mMati;
                else if (nm.StartsWith("Tiang") || nm.StartsWith("Alas") || nm.StartsWith("Lengan") || nm.StartsWith("Penyangga")) r.sharedMaterial = mTiang;
                else if (nm.StartsWith("Strip")) r.sharedMaterial = mStrip;
                else r.sharedMaterial = mBadan;
            }
        };
        foreach (var s in sigData)
        {
            int n = I(s[1]); int arm = I(s[2]);
            var kl = lampuNode[n];
            var g = (GameObject)PrefabUtility.InstantiatePrefab(fbxLalin, kl.transform);
            g.name = "Lampu Lalin " + n + "-" + arm;
            g.transform.position = new Vector3(F(s[3]), 0.27f, F(s[4]));
            g.transform.rotation = Quaternion.Euler(0f, F(s[5]), 0f);
            g.transform.localScale = new Vector3(-1f, 1f, 1f);     // lengan ke arah badan jalan
            bahanLampu(g);
            var ax = (nodes[arm] - nodes[n]); bool us = Mathf.Abs(ax.z) > Mathf.Abs(ax.x);
            foreach (var r in g.GetComponentsInChildren<Renderer>())
            {
                if (r.name.StartsWith("Merah")) tambah(kl, us ? "usM" : "tbM", r);
                if (r.name.StartsWith("Kuning")) tambah(kl, us ? "usK" : "tbK", r);
                if (r.name.StartsWith("Hijau")) tambah(kl, us ? "usH" : "tbH", r);
            }
        }
        foreach (var s in pedData)
        {
            int z = I(s[1]); int n = zNode[z];
            var kl = lampuNode[n];
            var g = (GameObject)PrefabUtility.InstantiatePrefab(fbxPeny, kl.transform);
            g.name = "Lampu Penyeberang " + z;
            g.transform.position = new Vector3(F(s[2]), 0.27f, F(s[3]));
            g.transform.rotation = Quaternion.Euler(0f, F(s[4]), 0f);
            bahanLampu(g);
            bool us = Mathf.Abs(zAx[z].z) > Mathf.Abs(zAx[z].x);
            foreach (var r in g.GetComponentsInChildren<Renderer>())
            {
                if (r.name.StartsWith("Merah")) tambah(kl, us ? "pusM" : "ptbM", r);
                if (r.name.StartsWith("Hijau")) tambah(kl, us ? "pusH" : "ptbH", r);
            }
        }
        foreach (var kv in kumpul)
        {
            var kl = kv.Key; var d = kv.Value;
            Renderer[] A(string k) => d.TryGetValue(k, out var x) ? x.ToArray() : new Renderer[0];
            kl.usMerah = A("usM"); kl.usKuning = A("usK"); kl.usHijau = A("usH");
            kl.tbMerah = A("tbM"); kl.tbKuning = A("tbK"); kl.tbHijau = A("tbH");
            kl.pUSMerah = A("pusM"); kl.pUSHijau = A("pusH"); kl.pTBMerah = A("ptbM"); kl.pTBHijau = A("ptbH");
        }

        lalin.nodes = nodes.ToArray(); lalin.edges = edges.ToArray(); lalin.nodeRadius = rad.ToArray();
        lalin.lampu = lampuNode.ToArray();
        lalin.zebraNode = zNode.ToArray(); lalin.zebraArm = zArm.ToArray(); lalin.zebraPos = zPos.ToArray(); lalin.zebraAxis = zAx.ToArray();
        lalin.pedNodes = pedN.ToArray(); lalin.pedEdges = pedE.ToArray();
        lalin.Build();

        // ---------- mobil ----------
        var jenisMobil = new[] {
            ("car_seperate_blue", 5.3f, 9.5f), ("car_seperate_red", 5.3f, 9.5f), ("car_seperate_green", 5.3f, 9.5f),
            ("taxi_seperate_mesh", 5.7f, 9.5f), ("van_seperate_blue", 5.6f, 8.5f), ("van_seperate_red", 5.6f, 8.5f),
            ("sedan_white_seperate_prefab", 5.7f, 10f), ("sedan_black_seperate_prefab", 5.7f, 10f), ("sedan_blue_seperate_prefab", 5.7f, 10f),
            ("car_seperate_green", 5.3f, 9.5f), ("bus_seperate_blue", 9.0f, 7.5f), ("taxi_seperate_mesh", 5.7f, 9.5f), ("bus_seperate_grey", 9.0f, 7.5f) };
        var klakson = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/AntarCepat/Audio/Antar_Klakson.wav");
        var mesin = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/AntarCepat/Audio/Antar_Mesin.wav");
        int ki = 0;
        foreach (var s in mobilData)
        {
            var j = jenisMobil[ki % jenisMobil.Length]; ki++;
            var pf = Prefab(j.Item1); if (pf == null) { log.AppendLine("mobil hilang " + j.Item1); continue; }
            var g = new GameObject("Mobil " + ki + " (" + j.Item1 + ")");
            g.transform.SetParent(tMobil, false);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(pf, g.transform);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);     // model menghadap -Z
            var km = g.AddComponent<KotaMobil>();
            km.traffic = lalin; km.fromNode = I(s[1]); km.toNode = I(s[2]); km.startProgress = F(s[3]);
            km.panjang = j.Item2; km.mundurGarisBerhenti = 3.8f + j.Item2 * 0.5f; km.maxSpeed = j.Item3 * Random.Range(0.92f, 1.08f);
            km.roda = model.GetComponentsInChildren<Transform>().Where(t => t.name.ToLower().Contains("wheel")).ToArray();
            km.suaraKlakson = klakson;
            var src = g.AddComponent<AudioSource>();
            src.clip = mesin; src.loop = true; src.playOnAwake = true; src.spatialBlend = 1f;
            src.rolloffMode = AudioRolloffMode.Linear; src.minDistance = 3f; src.maxDistance = 35f; src.volume = 0.3f; src.dopplerLevel = 0.3f;
            src.time = 0f;
            var p = lalin.TitikRuas(km.fromNode, km.toNode, km.startProgress);
            g.transform.position = new Vector3(p.x, tinggiJalan, p.z);
            g.transform.rotation = Quaternion.LookRotation(lalin.Dir(km.fromNode, km.toNode));
        }

        // ---------- warga ----------
        var namaWarga = new List<string>();
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab SimpleCitizens_")) namaWarga.Add(AssetDatabase.GUIDToAssetPath(guid));
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab SimplePeople_")) namaWarga.Add(AssetDatabase.GUIDToAssetPath(guid));
        string[] tolak = { "Prostitute", "Pimp", "Robber", "Prisoner", "RiotCop", "Luchador", "Clown", "BeachBabe", "Hobo" };
        namaWarga = namaWarga.Where(p => !tolak.Any(t => p.Contains(t))).ToList();
        var ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/SimpleWorldVol1/SimpleCitizens/Models/SimpleCitizens.controller");
        var rnd = new System.Random(5);
        int wi = 0;
        foreach (var s in wargaData)
        {
            var pf = AssetDatabase.LoadAssetAtPath<GameObject>(namaWarga[rnd.Next(namaWarga.Count)]);
            var g = new GameObject("Warga " + (++wi));
            g.transform.SetParent(tWarga, false);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(pf, g.transform);
            model.transform.localPosition = Vector3.zero; model.transform.localRotation = Quaternion.identity;
            foreach (var c in model.GetComponentsInChildren<Collider>()) c.enabled = false;
            // buang skrip bawaan prefab lain (mis. QuizMaster di salah satu prefab warga)
            foreach (var mb in model.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(mb);
            foreach (var a in model.GetComponentsInChildren<NavMeshAgent>()) a.enabled = false;
            var an = model.GetComponentInChildren<Animator>();
            if (an == null) an = model.AddComponent<Animator>();
            if (an.runtimeAnimatorController == null) an.runtimeAnimatorController = ctrl;
            an.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            var kw = g.AddComponent<KotaWarga>();
            kw.jalan = lalin; kw.edge = I(s[1]); kw.progres = F(s[2]); kw.maju = s[3] == "1";
            var e = pedE[kw.edge];
            var p = Vector3.Lerp(pedN[e.x], pedN[e.y], kw.progres);
            g.transform.position = p;
        }

        // ---------- langit ----------
        var tAwan = Anak(tLangit, "Awan");
        tAwan.AddComponent<KotaAwan>();
        var awanFbx = new GameObject[4];
        for (int i = 0; i < 4; i++) awanFbx[i] = AssetDatabase.LoadAssetAtPath<GameObject>(ROOT + "Model/Awan_" + (i + 1) + ".fbx");
        var rr = new System.Random(9);
        for (int i = 0; i < 34; i++)
        {
            float ang = (float)(rr.NextDouble() * Mathf.PI * 2); float jr = Mathf.Sqrt((float)rr.NextDouble()) * 640f;
            var pos = new Vector3(60f + Mathf.Cos(ang) * jr, 135f + (float)rr.NextDouble() * 70f, 55f + Mathf.Sin(ang) * jr);
            var g = (GameObject)PrefabUtility.InstantiatePrefab(awanFbx[i % 4], tAwan.transform);
            g.transform.position = pos;
            g.transform.rotation = Quaternion.Euler(0f, (float)rr.NextDouble() * 360f, 0f);
            float sk = 2.6f + (float)rr.NextDouble() * 2.8f;
            g.transform.localScale = new Vector3(sk, sk * (0.75f + (float)rr.NextDouble() * 0.4f), sk);
            foreach (var r in g.GetComponentsInChildren<Renderer>()) { r.sharedMaterial = mAwan; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false; }
        }
        var bukit = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ROOT + "Model/Bukit.fbx"), tLangit);
        bukit.transform.position = new Vector3(62f, -3f, 55f);
        foreach (var r in bukit.GetComponentsInChildren<Renderer>()) { r.sharedMaterials = new[] { mBukit, mBukitAtas }; r.shadowCastingMode = ShadowCastingMode.Off; }

        // ---------- NavMesh: objek bergerak, langit dan tanah jauh tidak ikut ----------
        foreach (var t in new[] { tMobil, tWarga, tLangit, jauh.transform })
        {
            var mod = t.gameObject.AddComponent<NavMeshModifier>(); mod.ignoreFromBuild = true;
        }

        log.AppendLine($"node {nodes.Count} edge {edges.Count} zebra {zPos.Count} bangunan {bangunan} pohon {pohon} lampu {lampu} mobil {mobilData.Count} warga {wargaData.Count}");
        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();
        return log.ToString();
    }
}
#endif
