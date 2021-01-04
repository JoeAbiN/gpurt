using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Main : MonoBehaviour {
    struct Sphere {
        public Vector3 position;
        public float radius;
        public Vector3 albedo;
        public Vector3 specular;
        public float smoothness;
        public Vector3 emission;
    };

    public ComputeShader shader;

    private Camera camera;
    private RenderTexture target;
    private RenderTexture converged;

    public Texture skyboxTex;

    public Light directionalLight;

    [Range(1, 8)]
    public int numTrace = 8;
    private uint currSample = 0;
    private Material addMaterial;

    public Text text;

    public Vector2 radius = new Vector2(3.0f, 8.0f);
    public uint numSpheres = 100;
    public float placementRadius = 100.0f;
    private ComputeBuffer sphereBuffer;

    public int sphereSeed;

    private void OnEnable() {
        currSample = 0;
        SetupScene();
    }

    private void OnDisable() {
        if (sphereBuffer != null)
            sphereBuffer.Release();
    }

    private void Awake() {
        camera = GetComponent<Camera>();
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
        shader.SetBuffer(0, "spheres", sphereBuffer);
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