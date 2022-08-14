using UnityEngine;

public class RayTracee : MonoBehaviour {
    #region Properties
    private MeshFilter _meshFilter;
    public MeshFilter meshFilter {
        get {
            if (!_meshFilter) _meshFilter = GetComponent<MeshFilter>();
            return _meshFilter;
        }
    }

    private Renderer _renderer;
    public new Renderer renderer {
        get {
            if (!_renderer) _renderer = GetComponent<Renderer>();
            return _renderer;
        }
    }
    #endregion

    public struct BoundingVolume {
        public Vector3 min;
        public Vector3 max;
    }

    private Bounds bounds;
    public BoundingVolume boundingVolume;

    void Awake() {
        bounds = meshFilter.sharedMesh.bounds;
        boundingVolume = new BoundingVolume();
        boundingVolume.min = transform.TransformPoint(bounds.min);
        boundingVolume.max = transform.TransformPoint(bounds.max);
    }

    private void OnEnable() {
        Main.RegisterObject(this);
    }

    private void OnDisable() {
        Main.RegisterObject(this);
    }
}