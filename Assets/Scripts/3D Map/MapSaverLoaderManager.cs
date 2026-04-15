using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using RosSharp.RosBridgeClient;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class MapSaverLoaderManager : MonoBehaviour
{
    [Header("Referencias")]
    public ParticleMap particleMap;
    public MeshMap meshMap;
    public MapSubscriber wallMap;

    [Header("Materiales")]
    public Material wallMaterial;

    [Header("Nombre archivo")]
    public string fileName = "SavedMap";

    private Transform loadedWallsParent;

    // =========================================================
    string AssetPath(string ext)
    {
#if UNITY_EDITOR
        string folder = "Assets/SavedMaps";
        if (!Directory.Exists(folder))
            Directory.CreateDirectory(folder);

        return $"{folder}/{fileName}{ext}";
#else
        return Path.Combine(Application.persistentDataPath, fileName + ext);
#endif
    }

    // =========================================================
    // UTIL: PARENT PARA WALLS CARGADAS
    // =========================================================
    private void EnsureLoadedParent()
    {
        if (loadedWallsParent != null) return;
        GameObject go = new GameObject("LoadedWalls");
        loadedWallsParent = go.transform;
    }

    // =========================================================
    // PREFAB WALLS
    // =========================================================
    [ContextMenu("Guardar Prefab")]
    public void SavePrefab()
    {
#if UNITY_EDITOR
        if (wallMap == null)
        {
            Debug.LogError("WallMap no asignado");
            return;
        }

        string path = AssetPath(".prefab");

        // copia temporal
        GameObject temp = Instantiate(wallMap.gameObject);
        temp.name = wallMap.gameObject.name + "_TEMP_PREFAB";

        //eliminar script
        foreach (var mb in temp.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (mb == null) continue;

            if (mb.GetType().Name == "MapSubscriber")
            {
                DestroyImmediate(mb);
            }
        }

        // cambiar materiale
        foreach (var r in temp.GetComponentsInChildren<Renderer>(true))
        {
            if (wallMaterial != null)
            {
                r.sharedMaterial = wallMaterial;
            }
        }

        // guardar prefab
        PrefabUtility.SaveAsPrefabAsset(temp, path);

        // borrar objeto temporal
        DestroyImmediate(temp);

        Debug.Log("Prefab guardado correctamente en: " + path);
#endif
    }

    // =========================================================
    // WALLS JSON
    // =========================================================
    [Serializable]
    public class WallData
    {
        public Vector3 pos;
        public Quaternion rot;
        public Vector3 scale;
    }

    [Serializable]
    public class WallWrapper
    {
        public List<WallData> walls;
    }

    [ContextMenu("Guardar Walls")]
    public void SaveWalls()
    {
        List<WallData> data = new();

        int index = 0;

        foreach (Transform child in wallMap.transform)
        {
            // ignorar plano base (primer hijo)
            if (index == 0)
            {
                index++;
                continue;
            }

            if (child.GetComponent<Collider>() != null &&
                child.GetComponent<ParticleMap>() == null &&
                child.GetComponent<MeshMap>() == null)
            {
                data.Add(new WallData
                {
                    pos = child.position,
                    rot = child.rotation,
                    scale = child.localScale
                });
            }

            index++;
        }

        File.WriteAllText(
            AssetPath("_walls.json"),
            JsonUtility.ToJson(new WallWrapper { walls = data }, true)
        );

        Debug.Log($"Walls guardadas: {data.Count}");
    }

    [ContextMenu("Cargar Walls")]
    public void LoadWalls()
    {
        string path = AssetPath("_walls.json");

        if (!File.Exists(path))
        {
            Debug.LogError("Archivo walls no encontrado");
            return;
        }

        EnsureLoadedParent();

        var wrapper = JsonUtility.FromJson<WallWrapper>(File.ReadAllText(path));

        foreach (var w in wrapper.walls)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);

            go.transform.position = w.pos;
            go.transform.rotation = w.rot;
            go.transform.localScale = w.scale;

            if (wallMaterial != null)
                go.GetComponent<Renderer>().material = wallMaterial;

            go.transform.SetParent(loadedWallsParent);
        }

        Debug.Log("Walls cargadas");
    }

    // =========================================================
    // PARTICLE MAP SAVE/LOAD
    // =========================================================
    [Serializable]
    public class ParticleData
    {
        public float x, y, z;
        public byte r, g, b, a;
    }

    [Serializable]
    public class ParticleWrapper
    {
        public List<ParticleData> particles;
    }

    [ContextMenu("Guardar ParticleMap")]
    public void SaveParticleMap()
    {
        if (particleMap == null)
        {
            Debug.LogError("ParticleMap no asignado");
            return;
        }

        var pts = particleMap.GetPoints();
        List<ParticleData> data = new(pts.Count);

        foreach (var p in pts)
        {
            data.Add(new ParticleData
            {
                x = p.pos.x,
                y = p.pos.y,
                z = p.pos.z,
                r = p.col.r,
                g = p.col.g,
                b = p.col.b,
                a = p.col.a
            });
        }

        File.WriteAllText(
            AssetPath("_particles.json"),
            JsonUtility.ToJson(new ParticleWrapper { particles = data }, true)
        );

        Debug.Log($"ParticleMap guardado: {data.Count}");
    }

    [ContextMenu("Cargar ParticleMap")]
    public void LoadParticleMap()
    {
        if (particleMap == null)
            return;

        string path = AssetPath("_particles.json");

        if (!File.Exists(path))
        {
            Debug.LogError("Archivo particle no encontrado");
            return;
        }

        particleMap.ClearAll();

        var wrapper = JsonUtility.FromJson<ParticleWrapper>(File.ReadAllText(path));

        foreach (var p in wrapper.particles)
        {
            particleMap.AddPoint(
                new Vector3(p.x, p.y, p.z),
                new Color32(p.r, p.g, p.b, p.a)
            );
        }

        Debug.Log("ParticleMap cargado");
    }

    // =========================================================
    // MESH MAP SAVE/LOAD
    // =========================================================
    [Serializable]
    public class MeshData
    {
        public float x, y, z;
        public byte r, g, b, a;
    }

    [Serializable]
    public class MeshWrapper
    {
        public List<MeshData> mesh;
    }

    [ContextMenu("Guardar MeshMap")]
    public void SaveMeshMap()
    {
        if (meshMap == null)
        {
            Debug.LogError("MeshMap no asignado");
            return;
        }

        var pts = meshMap.GetPoints();
        List<MeshData> data = new(pts.Count);

        foreach (var p in pts)
        {
            data.Add(new MeshData
            {
                x = p.pos.x,
                y = p.pos.y,
                z = p.pos.z,
                r = p.col.r,
                g = p.col.g,
                b = p.col.b,
                a = p.col.a
            });
        }

        File.WriteAllText(
            AssetPath("_mesh.json"),
            JsonUtility.ToJson(new MeshWrapper { mesh = data }, true)
        );

        Debug.Log($"MeshMap guardado: {data.Count}");
    }

    [ContextMenu("Cargar MeshMap")]
    public void LoadMeshMap()
    {
        if (meshMap == null)
            return;

        string path = AssetPath("_mesh.json");

        if (!File.Exists(path))
        {
            Debug.LogError("Archivo mesh no encontrado");
            return;
        }

        meshMap.ClearAll();

        var wrapper = JsonUtility.FromJson<MeshWrapper>(File.ReadAllText(path));

        foreach (var m in wrapper.mesh)
        {
            meshMap.AddPoint(
                new Vector3(m.x, m.y, m.z),
                new Color32(m.r, m.g, m.b, m.a)
            );
        }

        Debug.Log("MeshMap cargado");
    }
}
