using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using BoundingVolume = RayTracee.BoundingVolume;

public class Main : MonoBehaviour {
    struct Sphere {
        public Vector3 position;
        public float radius;
        public Vector3 albedo;
        public Vector3 specular;
        public float smoothness;
        public Vector3 emission;
    };

    struct MeshObject {
        public Matrix4x4 objMatrix;
        public int indicesOffset;
        public int indicesCount;
        public Vector3 boundsMin;
        public Vector3 boundsMax;
    }

    public ComputeShader shader;

    // Rendering
    private new Camera camera;
    private RenderTexture target;
    private RenderTexture converged;

    // Bouncing and sampling
    [Range(1, 8)]
    public int numTrace = 8;
    private uint currSample = 0;
    private Material addMaterial;

    // Spheres
    public Vector2 radius = new Vector2(3.0f, 8.0f);
    public uint numSpheres = 100;
    public float placementRadius = 100.0f;
    private ComputeBuffer sphereBuffer;
    public int sphereSeed;

    // Objects
    private static bool objsNeedRebuildig = false;
    private static List<RayTracee> objs = new List<RayTracee>();

    private static List<MeshObject> meshObjects = new List<MeshObject>();
    // private static List<BoundingVolume> boundingVolumes = new List<BoundingVolume>();
    private static List<Vector3> vertices = new List<Vector3>();
    private static List<int> indices = new List<int>();
    private static List<Vector3> normals = new List<Vector3>();

    private ComputeBuffer meshObjectBuffer;
    // private ComputeBuffer boundingVolumeBuffer;
    private ComputeBuffer vertexBuffer;
    private ComputeBuffer indexBuffer;
    private ComputeBuffer normalBuffer;

    // Some params
    public Texture skyboxTex;
    public Light directionalLight;

    // UI
    public Text text;

    private void OnEnable() {
        currSample = 0;
        SetupScene();
    }

    private void OnDisable() {
        if (sphereBuffer != null)
            sphereBuffer.Release();

        if (meshObjectBuffer != null)
            meshObjectBuffer.Release();

        if (vertexBuffer != null)
            vertexBuffer.Release();

        if (indexBuffer != null)
            indexBuffer.Release();

        if (normalBuffer != null)
            normalBuffer.Release();
    }

    private void Awake() {
        camera = GetComponent<Camera>();
    }

    private static void CreateComputeBuffer<T> (ref ComputeBuffer buffer, List<T> data, int stride) where T : struct {
        // Do we already have a compute buffer?
        if (buffer != null) {
            // If no data or buffer doesn't match the given criteria, release it
            if (data.Count == 0 || buffer.count != data.Count || buffer.stride != stride) {
                buffer.Release();
                buffer = null;
            }
        }

        if (data.Count != 0) {
            // If the buffer has been released or wasn't there to
            // begin with, create it
            if (buffer == null) {
                buffer = new ComputeBuffer(data.Count, stride);
            }
            // Set data on the buffer
            buffer.SetData(data);
        }
    }

    private void SetComputeBuffer(string name, ComputeBuffer buffer) {
        if (buffer != null) {
            shader.SetBuffer(0, name, buffer);
        }
    }

    public static void RegisterObject(RayTracee obj) {
        objs.Add(obj);
        objsNeedRebuildig = true;
    }

    public static void UnregisterObject(RayTracee obj) {
        objs.Remove(obj);
        objsNeedRebuildig = true;
    }

    private void RebuildMeshObjectBuffers() {
        if (!objsNeedRebuildig)
            return;

        objsNeedRebuildig = false;
        currSample = 0;

        // Clear lists
        meshObjects.Clear();
        vertices.Clear();
        indices.Clear();
        normals.Clear();

        // Get game objects data
        foreach (RayTracee rayTracee in objs) {
            Mesh mesh = rayTracee.meshFilter.sharedMesh;

            // Add vertex data
            int firstVertex = vertices.Count;
            vertices.AddRange(mesh.vertices);

            // Add index data and offset indices if vertex buffer isn't empty
            int firstIndex = indices.Count;
            indices.AddRange(mesh.GetIndices(0).Select(index => index + firstVertex));

            // Add normals data
            normals.AddRange(mesh.normals);

            // Add mesh object data
            meshObjects.Add(new MeshObject() {
                objMatrix = rayTracee.transform.localToWorldMatrix,
                indicesOffset = firstIndex,
                indicesCount = mesh.GetIndices(0).Length,
                boundsMin = rayTracee.boundingVolume.min,
                boundsMax = rayTracee.boundingVolume.max
            });
        }

        // Set up buffers
        CreateComputeBuffer(ref meshObjectBuffer, meshObjects, 96);
        // CreateComputeBuffer(ref boundingVolumeBuffer, boundingVolumes, 24);
        CreateComputeBuffer(ref vertexBuffer, vertices, 12);
        CreateComputeBuffer(ref indexBuffer, indices, 4);
        CreateComputeBuffer(ref normalBuffer, normals, 12);
    }

    private void SetupScene() {
        Random.InitState(sphereSeed);

        List<Sphere> spheres = new List<Sphere>();
        
        // Add a number of random spheres
        for (int i = 0; i < numSpheres; i++) {
            Sphere sphere = new Sphere();
            
            // Radius and radius
            sphere.radius = radius.x + Random.value * (radius.y - radius.x);
            Vector2 randomPos = Random.insideUnitCircle * placementRadius;
            sphere.position = new Vector3(randomPos.x, sphere.radius, randomPos.y);
            
            // Reject spheres that are intersecting others
            foreach (Sphere other in spheres) {
                float minDist = sphere.radius + other.radius;
                if (Vector3.SqrMagnitude(sphere.position - other.position) < minDist * minDist)
                    goto SkipSphere;
            }
            
            // Set shader properties
            Color color = Random.ColorHSV();
            bool metal = Random.value < 0.5f;
            sphere.albedo = metal ? Vector3.zero : new Vector3(color.r, color.g, color.b);
            sphere.specular = metal ? new Vector3(color.r, color.g, color.b) : Vector3.one * 0.04f;
            sphere.smoothness = Random.value;
            sphere.emission = new Vector3(Random.value, Random.value, Random.value);

            // Add the sphere to the list
            spheres.Add(sphere);
        
            SkipSphere:
                continue;
        }

        // Assign to compute buffer
        sphereBuffer = new ComputeBuffer(spheres.Count, 56);
        sphereBuffer.SetData(spheres);
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination) {
        RebuildMeshObjectBuffers();
        SetShaderParams();
        Render(destination);
    }

    private void SetShaderParams() {
        shader.SetMatrix("camToWorld", camera.cameraToWorldMatrix);
        shader.SetMatrix("camInvProj", camera.projectionMatrix.inverse);
        shader.SetTexture(0, "skyboxTex", skyboxTex);
        shader.SetVector("pixelOffset", new Vector2(Random.value, Random.value));
        shader.SetFloat("seed", Random.value);
        shader.SetVector("lightDir", directionalLight.transform.forward);
        shader.SetFloat("lightIntensity", directionalLight.intensity);
        shader.SetInt("numTrace", numTrace);
        
        //shader.SetBuffer(0, "spheres", sphereBuffer);
        SetComputeBuffer("spheres", sphereBuffer);
        SetComputeBuffer("meshObjects", meshObjectBuffer);
        SetComputeBuffer("vertices", vertexBuffer);
        SetComputeBuffer("indices", indexBuffer);
        SetComputeBuffer("normals", normalBuffer);
    }

    private void Render(RenderTexture destination) {
        // Make sure we have a current render target
        InitRenderTexture();

        // Set the target and dispatch the compute shader
        shader.SetTexture(0, "Result", target);
        int threadGroupsX = Mathf.CeilToInt(Screen.width / 8.0f);
        int threadGroupsY = Mathf.CeilToInt(Screen.height / 8.0f);
        shader.Dispatch(0, threadGroupsX, threadGroupsY, 1);

        // Blit the result texture to the screen
        if (!addMaterial)
            addMaterial = new Material(Shader.Find("Hidden/AddShader"));
        addMaterial.SetFloat("_Sample", currSample);
        Graphics.Blit(target, converged, addMaterial);
        Graphics.Blit(converged, destination);
        currSample++;
    }

    private void InitRenderTexture() {
        if (target == null || target.width != Screen.width || target.height != Screen.height) {
            // Release render texture if we already have one
            if (target != null) {
                target.Release();
                converged.Release();
            }

            // Get a render target for Ray Tracing
            target = new RenderTexture(Screen.width, Screen.height, 0,
                RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            target.enableRandomWrite = true;
            target.Create();

            converged = new RenderTexture(Screen.width, Screen.height, 0,
                RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            converged.enableRandomWrite = true;
            converged.Create();
        }
    }

    private void Update() {
        if (transform.hasChanged) {
            currSample = 0;
            transform.hasChanged = false;
        }

        if (Input.GetKeyDown(KeyCode.UpArrow) && numTrace < 8) {
            numTrace++;
            shader.SetInt("numTrace", numTrace);
        
        } else if (Input.GetKeyDown(KeyCode.DownArrow) && numTrace > 1) {
            numTrace--;
            shader.SetInt("numTrace", numTrace);
        }


        text.text = "Number of reflections: " + (numTrace - 1);
    }
}