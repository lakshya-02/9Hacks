using UnityEngine;
using GLTFast;

public class ModelLoader : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform spawnPoint;

    private GameObject _currentModel;

    public async void LoadModel(byte[] glbBytes)
    {
        // Destroy previous model if one exists
        if (_currentModel != null)
        {
            Destroy(_currentModel);
        }

        var gltf = new GltfImport();
        bool success = await gltf.Load(glbBytes);

        if (!success)
        {
            Debug.LogError("[ModelLoader] Failed to load .glb data.");
            return;
        }

        var parent = new GameObject("SpawnedModel");
        parent.transform.SetPositionAndRotation(spawnPoint.position, spawnPoint.rotation);

        bool instantiated = await gltf.InstantiateMainSceneAsync(parent.transform);

        if (!instantiated)
        {
            Debug.LogError("[ModelLoader] Failed to instantiate glTF scene.");
            Destroy(parent);
            return;
        }

        // Add physics components for future Interaction SDK grab
        parent.AddComponent<BoxCollider>();
        var rb = parent.AddComponent<Rigidbody>();
        rb.isKinematic = true;

        _currentModel = parent;
        Debug.Log("[ModelLoader] Model loaded and placed at spawn point.");
    }
}
