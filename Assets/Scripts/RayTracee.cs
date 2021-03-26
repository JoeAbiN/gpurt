using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class RayTracee : MonoBehaviour {
    private void OnEnable() {
        Main.RegisterObject(this);
    }

    private void OnDisable() {
        Main.RegisterObject(this);
    }
}