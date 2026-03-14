using UnityEngine;
using GLTFast;

public class ModelLoader : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform spawnPoint;

    private GameObject _currentModel;

    public async void LoadModel(byte[] glbBytes)
    {
        Debug.Log("[ModelLoader] Starting model load...");

        if (_currentModel != null)
            Destroy(_currentModel);

        var gltf = new GltfImport();
        bool success = await gltf.Load(glbBytes);

        if (!success)
        {
            Debug.LogWarning("[ModelLoader] Failed to load .glb data.");
            return;
        }

        var parent = new GameObject("SpawnedModel");
        if (spawnPoint != null)
        {
            parent.transform.SetPositionAndRotation(spawnPoint.position, spawnPoint.rotation);
        }
        else
        {
            parent.transform.position = Camera.main != null
                ? Camera.main.transform.position + Camera.main.transform.forward * 1.5f
                : Vector3.forward * 1.5f;
        }

        bool instantiated = await gltf.InstantiateMainSceneAsync(parent.transform);

        if (!instantiated)
        {
            Debug.LogWarning("[ModelLoader] Failed to instantiate glTF scene.");
            Destroy(parent);
            return;
        }

        MakeGrabbable(parent);

        _currentModel = parent;
        Debug.Log("[ModelLoader] Model loaded with full grab support!");
    }

    private void MakeGrabbable(GameObject obj)
    {
        // 1. Add MeshCollider on every child that has a MeshFilter (tight fit)
        foreach (var mf in obj.GetComponentsInChildren<MeshFilter>())
        {
            if (mf.sharedMesh != null && mf.GetComponent<Collider>() == null)
            {
                var mc = mf.gameObject.AddComponent<MeshCollider>();
                mc.convex = true;
            }
        }

        // 2. Also add a BoxCollider on root as fallback
        if (obj.GetComponent<Collider>() == null)
            obj.AddComponent<BoxCollider>();

        // 3. Rigidbody — kinematic so it doesn't fall, but grabbable
        var rb = obj.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        // 4. Add OVRGrabbable or Interaction SDK Grabbable
        try
        {
            System.Type grabbableType = null;
            System.Type grabInteractableType = null;

            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                // Option A: Legacy OVRGrabbable
                if (grabbableType == null)
                    grabbableType = asm.GetType("OVRGrabbable");

                // Option B: Interaction SDK
                if (grabbableType == null)
                    grabbableType = asm.GetType("Oculus.Interaction.Grabbable");
                if (grabInteractableType == null)
                    grabInteractableType = asm.GetType("Oculus.Interaction.GrabInteractable");
            }

            if (grabbableType != null)
            {
                obj.AddComponent(grabbableType);
                Debug.Log($"[ModelLoader] Added {grabbableType.Name}");
            }

            // GrabInteractable is needed alongside Grabbable in Interaction SDK
            if (grabInteractableType != null)
            {
                obj.AddComponent(grabInteractableType);
                Debug.Log($"[ModelLoader] Added {grabInteractableType.Name}");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[ModelLoader] Grab component skipped: {e.Message}");
        }

        // 5. Set layer to "Default" so raycasts hit it
        obj.layer = 0;
        foreach (Transform child in obj.GetComponentsInChildren<Transform>())
            child.gameObject.layer = 0;

        Debug.Log("[ModelLoader] Grab setup complete: Rigidbody + Colliders + Grabbable");
    }
}
