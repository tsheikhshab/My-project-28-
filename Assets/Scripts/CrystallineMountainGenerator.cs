using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class WireframeCrystalMountainGenerator : MonoBehaviour
{
    // Mountain Cone Parameters
    [Header("Mountain Structure")]
    [Range(3, 16)]
    public int numberOfCones = 5;
    [Range(8, 72)]
    public int coneResolution = 36;
    public float baseRadius = 20f;
    public float baseHeight = 15f;
    public float radiusDecrement = 3f;
    public float heightDecrement = 2f;

    // Vein Structure Parameters  
    [Header("Crystal Veins")]
    public int mainBranches = 12;
    public int subBranchLevels = 3;
    public int branchesPerLevel = 3;
    public float branchThickness = 0.4f;
    public float branchLengthFactor = 0.7f;
    public float branchRandomness = 0.2f;

    // Wireframe Parameters
    [Header("Wireframe Appearance")]
    public Color wireframeColor = new Color(0.8f, 0.9f, 1.0f, 0.8f); // Bright visible lines
    public float wireframeThickness = 0.05f; // Line thickness
    public float wireframeEmissionIntensity = 2.5f; // Bright emission for lines

    // Crystal Effects (now mostly for veins)
    [Header("Crystal Appearance")]
    public Color crystalColor = new Color(0.8f, 0.9f, 1.0f, 0.05f); // Nearly invisible faces
    public Color emissionColor = new Color(0.9f, 0.95f, 1.0f, 1.0f); // For veins
    public float emissionIntensity = 1.5f; // For veins
    public float crystalRoughness = 0.05f;
    public float crystalMetallic = 0.0f;

    // Internal Variables
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private MeshFilter wireframeMeshFilter;
    private MeshRenderer wireframeMeshRenderer;
    private GameObject wireframeObject;
    private List<Vector3> vertices;
    private List<int> triangles;
    private List<Vector3> normals;
    private List<Vector2> uvs;
    private List<Color> colors;
    private Material crystalMaterial;
    private Material wireframeMaterial;

    // For wireframe edges
    private List<Vector3> edgeVertices;
    private List<int> edgeIndices;
    private List<Color> edgeColors;
    private HashSet<EdgeKey> processedEdges;

    // Fractal vein branch structure
    private class Branch
    {
        public Vector3 start;
        public Vector3 end;
        public float thickness;
        public Color color;
        public List<Branch> subBranches = new List<Branch>();
    }

    // Structure to track unique edges
    private struct EdgeKey
    {
        public int v1;
        public int v2;

        public EdgeKey(int vertex1, int vertex2)
        {
            // Store vertices in order for consistent hashing
            if (vertex1 < vertex2)
            {
                v1 = vertex1;
                v2 = vertex2;
            }
            else
            {
                v1 = vertex2;
                v2 = vertex1;
            }
        }

        public override bool Equals(object obj)
        {
            if (!(obj is EdgeKey)) return false;
            EdgeKey other = (EdgeKey)obj;
            return v1 == other.v1 && v2 == other.v2;
        }

        public override int GetHashCode()
        {
            return v1.GetHashCode() ^ (v2.GetHashCode() << 1);
        }
    }

    private List<Branch> mainVeinBranches = new List<Branch>();

    void Start()
    {
        // Try to enable VR mode if available
        try
        {
            XRSettings.enabled = true;
        }
        catch (System.Exception)
        {
            Debug.Log("XR not available or not enabled");
        }

        // Get components
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();

        // Create a child GameObject for the wireframe
        wireframeObject = new GameObject("Wireframe");
        wireframeObject.transform.SetParent(transform, false);
        wireframeMeshFilter = wireframeObject.AddComponent<MeshFilter>();
        wireframeMeshRenderer = wireframeObject.AddComponent<MeshRenderer>();

        // Initialize the mountain generation
        GenerateCrystallineMountain();

        // Position camera much closer to the mountain
        if (Camera.main != null)
        {
            Camera.main.transform.position = new Vector3(0, baseHeight / 2, -baseRadius * 0.7f);
            Camera.main.transform.LookAt(new Vector3(0, baseHeight / 3, 0));

            // Add subtle ambient light to make structure more visible
            RenderSettings.ambientLight = new Color(0.1f, 0.1f, 0.2f);
            RenderSettings.ambientIntensity = 1.2f;
        }
    }

    public void GenerateCrystallineMountain()
    {
        // Initialize collections
        vertices = new List<Vector3>();
        triangles = new List<int>();
        normals = new List<Vector3>();
        uvs = new List<Vector2>();
        colors = new List<Color>();

        // Initialize edge collections
        edgeVertices = new List<Vector3>();
        edgeIndices = new List<int>();
        edgeColors = new List<Color>();
        processedEdges = new HashSet<EdgeKey>();

        // Create the concentric mountain cones
        CreateConcentricCones();

        // Generate the vein structure
        GenerateVeinStructure();

        // Create a unified mesh from all geometry
        CreateUnifiedMesh();

        // Create wireframe mesh
        CreateWireframeMesh();

        // Setup crystal materials
        SetupCrystalMaterial();
        SetupWireframeMaterial();
    }

    private void CreateConcentricCones()
    {
        float currentRadius = baseRadius;
        float currentHeight = baseHeight;

        // Generate each concentric cone
        for (int coneIndex = 0; coneIndex < numberOfCones; coneIndex++)
        {
            int baseVertexIndex = vertices.Count;

            // Create bottom circle vertices
            for (int i = 0; i < coneResolution; i++)
            {
                float angle = i * (2 * Mathf.PI / coneResolution);
                float x = Mathf.Cos(angle) * currentRadius;
                float z = Mathf.Sin(angle) * currentRadius;

                // Add some subtle variation to make it more natural
                float radiusVariation = 1.0f + (Mathf.PerlinNoise(x * 0.1f, z * 0.1f) - 0.5f) * 0.2f;

                vertices.Add(new Vector3(x * radiusVariation, 0, z * radiusVariation));
                normals.Add(new Vector3(x, 0, z).normalized);
                uvs.Add(new Vector2((float)i / coneResolution, 0));

                // Nearly invisible color for face
                Color baseColor = new Color(0.85f, 0.95f, 1.0f, 0.02f);
                colors.Add(baseColor);
            }

            // Add peak vertex
            vertices.Add(new Vector3(0, currentHeight, 0));
            normals.Add(Vector3.up);
            uvs.Add(new Vector2(0.5f, 1.0f));

            // Nearly invisible color for peak
            Color peakColor = new Color(1.0f, 0.7f, 1.0f, 0.02f);
            colors.Add(peakColor);

            int peakIndex = vertices.Count - 1;

            // Create triangles and track edges for wireframe
            for (int i = 0; i < coneResolution; i++)
            {
                int currentIndex = baseVertexIndex + i;
                int nextIndex = baseVertexIndex + ((i + 1) % coneResolution);

                // Side faces
                triangles.Add(currentIndex);
                triangles.Add(nextIndex);
                triangles.Add(peakIndex);

                // Track edges for wireframe
                AddEdge(currentIndex, nextIndex);
                AddEdge(currentIndex, peakIndex);
                AddEdge(nextIndex, peakIndex);

                // Add crystal facets along the cone sides - more on outer cones
                float crystalChance = coneIndex == 0 ? 0.4f : (coneIndex == numberOfCones - 1 ? 0.9f : 0.6f);
                if (Random.value > 1.0f - crystalChance)
                {
                    // Create a crystal protrusion
                    int crystalBaseIndex = vertices.Count;
                    float crystalHeight = currentHeight * 0.3f * Random.Range(0.7f, 1.3f);
                    float crystalWidth = currentRadius * 0.15f * Random.Range(0.7f, 1.3f);

                    Vector3 midPoint = (vertices[currentIndex] + vertices[nextIndex]) * 0.5f;
                    Vector3 outDirection = midPoint.normalized;
                    Vector3 upDirection = new Vector3(0, 1, 0);

                    // Crystal point
                    Vector3 crystalTip = midPoint + outDirection * crystalWidth + upDirection * crystalHeight;

                    // Add vertices for crystal
                    vertices.Add(vertices[currentIndex]); // Reuse existing vertex
                    vertices.Add(vertices[nextIndex]); // Reuse existing vertex
                    vertices.Add(crystalTip);

                    // Add normals for crystal facet
                    Vector3 crystalNormal = Vector3.Cross(
                        vertices[nextIndex] - vertices[currentIndex],
                        crystalTip - vertices[currentIndex]
                    ).normalized;

                    normals.Add(crystalNormal);
                    normals.Add(crystalNormal);
                    normals.Add(crystalNormal);

                    // Add UVs
                    uvs.Add(new Vector2(0, 0));
                    uvs.Add(new Vector2(1, 0));
                    uvs.Add(new Vector2(0.5f, 1));

                    // Nearly invisible crystal facets
                    Color crystalFaceColor = new Color(0.95f, 0.98f, 1.0f, 0.02f);

                    colors.Add(crystalFaceColor);
                    colors.Add(crystalFaceColor);
                    colors.Add(crystalFaceColor);

                    // Add triangles
                    int cv1 = crystalBaseIndex;
                    int cv2 = crystalBaseIndex + 1;
                    int cv3 = crystalBaseIndex + 2;

                    triangles.Add(cv1);
                    triangles.Add(cv2);
                    triangles.Add(cv3);

                    // Track edges for wireframe
                    AddEdge(cv1, cv2);
                    AddEdge(cv1, cv3);
                    AddEdge(cv2, cv3);
                }
            }

            // Update for next cone
            currentRadius -= radiusDecrement;
            currentHeight -= heightDecrement;
        }
    }

    private void AddEdge(int v1, int v2)
    {
        EdgeKey edge = new EdgeKey(v1, v2);
        if (!processedEdges.Contains(edge))
        {
            processedEdges.Add(edge);

            // Add this edge to the wireframe mesh
            int baseIndex = edgeVertices.Count;

            // Add the two vertices that define this edge
            edgeVertices.Add(vertices[v1]);
            edgeVertices.Add(vertices[v2]);

            // Add indices for line drawing
            edgeIndices.Add(baseIndex);
            edgeIndices.Add(baseIndex + 1);

            // Add bright colors for the wireframe
            edgeColors.Add(wireframeColor);
            edgeColors.Add(wireframeColor);
        }
    }

    private void GenerateVeinStructure()
    {
        // Create the main vertical vein
        Vector3 mountainCenter = new Vector3(0, 0, 0);
        Vector3 mountainPeak = new Vector3(0, baseHeight, 0);

        // Generate main branches
        for (int i = 0; i < mainBranches; i++)
        {
            // Calculate starting point along central axis
            float heightRatio = Random.Range(0.0f, 0.8f);
            Vector3 branchStart = Vector3.Lerp(mountainCenter, mountainPeak, heightRatio);

            // Calculate random direction angled upward
            Vector3 randomDir = new Vector3(
                Random.Range(-1.0f, 1.0f),
                Random.Range(0.2f, 0.8f),
                Random.Range(-1.0f, 1.0f)
            ).normalized;

            // Calculate branch length based on distance to outer cone
            float distanceToEdge = DistanceToOuterCone(branchStart, randomDir);
            float branchLength = distanceToEdge * branchLengthFactor;

            // Create vibrant white light for veins
            Color branchColor = new Color(
                1.0f,  // Pure white base
                1.0f,
                1.0f,
                0.95f  // Highly visible
            );

            // Create branch
            Branch mainBranch = new Branch
            {
                start = branchStart,
                end = branchStart + randomDir * branchLength,
                thickness = branchThickness,
                color = branchColor
            };

            mainVeinBranches.Add(mainBranch);

            // Generate sub-branches recursively
            GenerateSubBranches(mainBranch, subBranchLevels);
        }

        // Create mesh for all branches
        CreateBranchMeshes();
    }

    private void GenerateSubBranches(Branch parentBranch, int levelsRemaining)
    {
        if (levelsRemaining <= 0) return;

        Vector3 parentDir = (parentBranch.end - parentBranch.start).normalized;
        float parentLength = Vector3.Distance(parentBranch.start, parentBranch.end);

        for (int i = 0; i < branchesPerLevel; i++)
        {
            // Calculate sub-branch starting point
            float startRatio = Random.Range(0.3f, 0.8f);
            Vector3 branchStart = Vector3.Lerp(parentBranch.start, parentBranch.end, startRatio);

            // Calculate random direction, biased upward and away from parent
            Vector3 randomDir = new Vector3(
                Random.Range(-1.0f, 1.0f),
                Random.Range(0.1f, 0.5f),
                Random.Range(-1.0f, 1.0f)
            ).normalized;

            // Blend with parent direction to prevent sharp angles
            randomDir = Vector3.Lerp(parentDir, randomDir, branchRandomness).normalized;

            // Calculate branch length
            float distanceToEdge = DistanceToOuterCone(branchStart, randomDir);
            float branchLength = distanceToEdge * branchLengthFactor * (0.6f - (0.1f * (subBranchLevels - levelsRemaining)));

            // Create sub-branch with slightly warmer white
            Color subBranchColor = new Color(
                1.0f,
                0.98f,
                0.95f,
                0.9f
            );

            // Create branch
            Branch subBranch = new Branch
            {
                start = branchStart,
                end = branchStart + randomDir * branchLength,
                thickness = parentBranch.thickness * 0.7f,
                color = subBranchColor
            };

            parentBranch.subBranches.Add(subBranch);

            // Recursively generate more sub-branches
            GenerateSubBranches(subBranch, levelsRemaining - 1);
        }
    }

    private float DistanceToOuterCone(Vector3 point, Vector3 direction)
    {
        // Simplified calculation - approximate distance to outer cone surface
        float horizontalDistance = Mathf.Sqrt(point.x * point.x + point.z * point.z);
        float remainingRadius = baseRadius - horizontalDistance;

        float heightRatio = point.y / baseHeight;
        float coneRadiusAtHeight = baseRadius * (1 - heightRatio);

        // Just return an approximate reasonable distance
        return Mathf.Min(remainingRadius, coneRadiusAtHeight) * 1.5f;
    }

    private void CreateBranchMeshes()
    {
        // Process all main branches and their sub-branches
        foreach (var branch in mainVeinBranches)
        {
            CreateBranchSegmentMesh(branch);
            CreateBranchesRecursively(branch);
        }
    }

    private void CreateBranchesRecursively(Branch branch)
    {
        foreach (var subBranch in branch.subBranches)
        {
            CreateBranchSegmentMesh(subBranch);
            CreateBranchesRecursively(subBranch);
        }
    }

    private void CreateBranchSegmentMesh(Branch branch)
    {
        int segments = 5; // Segments along branch
        int sides = 6;    // Cross-section vertices

        Vector3 direction = (branch.end - branch.start).normalized;

        // Create an orthogonal direction for cross-section
        Vector3 ortho = Vector3.Cross(direction, Vector3.up).normalized;
        if (ortho.magnitude < 0.1f)
        {
            ortho = Vector3.Cross(direction, Vector3.right).normalized;
        }

        Vector3 ortho2 = Vector3.Cross(direction, ortho).normalized;

        int baseIndex = vertices.Count;

        // Create vertices along the branch
        for (int i = 0; i <= segments; i++)
        {
            float t = (float)i / segments;
            Vector3 center = Vector3.Lerp(branch.start, branch.end, t);

            // Branch gets thinner toward the end
            float thickness = branch.thickness * (1 - t * 0.3f);

            // Add cross-section vertices
            for (int j = 0; j < sides; j++)
            {
                float angle = j * (2 * Mathf.PI / sides);
                Vector3 offset = ortho * Mathf.Cos(angle) * thickness + ortho2 * Mathf.Sin(angle) * thickness;

                vertices.Add(center + offset);

                // Calculate normal pointing outward from branch center
                normals.Add(offset.normalized);

                // Simple UVs
                uvs.Add(new Vector2((float)j / sides, t));

                // Add vein white light color
                Color vertexColor = new Color(
                    1.0f,
                    1.0f,
                    1.0f,
                    0.95f
                );
                colors.Add(vertexColor);
            }
        }

        // Create triangles
        for (int i = 0; i < segments; i++)
        {
            int ringStart = baseIndex + i * sides;
            int nextRingStart = baseIndex + (i + 1) * sides;

            for (int j = 0; j < sides; j++)
            {
                int nextJ = (j + 1) % sides;

                // Triangle 1
                triangles.Add(ringStart + j);
                triangles.Add(nextRingStart + j);
                triangles.Add(ringStart + nextJ);

                // Triangle 2
                triangles.Add(ringStart + nextJ);
                triangles.Add(nextRingStart + j);
                triangles.Add(nextRingStart + nextJ);
            }
        }
    }

    private void CreateUnifiedMesh()
    {
        Mesh mesh = new Mesh();

        // Check if we're approaching the 65K vertex limit for 16-bit index formats
        if (vertices.Count > 60000)
        {
            Debug.LogWarning("Approaching Unity's default 65K vertex limit. Consider splitting the mesh.");
            // In production, you'd implement mesh splitting here
        }

        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uvs);
        mesh.SetColors(colors);

        mesh.RecalculateBounds();

        // Assign the mesh to the mesh filter
        meshFilter.mesh = mesh;
    }

    private void CreateWireframeMesh()
    {
        Mesh wireframeMesh = new Mesh();

        wireframeMesh.SetVertices(edgeVertices);
        wireframeMesh.SetIndices(edgeIndices.ToArray(), MeshTopology.Lines, 0);
        wireframeMesh.SetColors(edgeColors);

        wireframeMesh.RecalculateBounds();

        // Assign the mesh to the wireframe mesh filter
        wireframeMeshFilter.mesh = wireframeMesh;
    }

    private void SetupCrystalMaterial()
    {
        // Configure scene for better visibility against navy background
        RenderSettings.fogColor = new Color(0.1f, 0.1f, 0.3f);
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.2f, 0.2f, 0.4f);
        RenderSettings.ambientEquatorColor = new Color(0.2f, 0.2f, 0.4f);
        RenderSettings.ambientGroundColor = new Color(0.1f, 0.1f, 0.3f);

        // Try to use Standard shader first for best glass effect
        Shader shader = Shader.Find("Standard");

        // Fall back to URP if Standard not found
        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/Lit");
        }

        // Last resort - use default
        if (shader == null)
        {
            shader = Shader.Find("Diffuse");
        }

        // Create the crystal material
        crystalMaterial = new Material(shader);

        // Set material properties based on shader
        if (shader.name == "Standard")
        {
            // Standard shader properties for glass-like appearance
            crystalMaterial.SetFloat("_Mode", 3); // Transparent mode
            crystalMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
            crystalMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            crystalMaterial.SetInt("_ZWrite", 1); // Enable Z-writing for proper glass depth
            crystalMaterial.DisableKeyword("_ALPHATEST_ON");
            crystalMaterial.EnableKeyword("_ALPHABLEND_ON");
            crystalMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");

            // Nearly invisible crystal color for faces
            crystalMaterial.SetColor("_Color", crystalColor);
            crystalMaterial.SetFloat("_Glossiness", 0.95f); // High gloss for glass
            crystalMaterial.SetFloat("_GlossMapScale", 0.98f); // High gloss
            crystalMaterial.SetFloat("_Metallic", 0.1f); // Low metallic for glass

            // Enable emission for the veins
            crystalMaterial.EnableKeyword("_EMISSION");
            crystalMaterial.SetColor("_EmissionColor", Color.white * 3.0f); // Strong white emission for veins
        }
        else if (shader.name.Contains("Universal Render Pipeline"))
        {
            // URP shader properties for glass
            crystalMaterial.SetColor("_BaseColor", crystalColor);
            crystalMaterial.SetFloat("_Smoothness", 0.95f); // Very smooth for glass
            crystalMaterial.SetFloat("_Metallic", 0.1f); // Low metallic for glass

            // Enable emission for veins
            crystalMaterial.EnableKeyword("_EMISSION");
            crystalMaterial.SetColor("_EmissionColor", Color.white * 3.0f);

            // Transparent surface settings
            crystalMaterial.SetFloat("_Surface", 1); // 1 = Transparent
            crystalMaterial.SetFloat("_Blend", 5);   // 5 = Alpha Premultiply for glass
            crystalMaterial.EnableKeyword("_ALPHAPREMULTIPLY_ON");
        }
        else
        {
            // Diffuse shader (simplest) - basic glass approximation
            crystalMaterial.SetColor("_Color", new Color(
                crystalColor.r,
                crystalColor.g,
                crystalColor.b,
                0.05f // Nearly invisible
            ));
        }

        // Enable vertex colors for all shader types
        crystalMaterial.EnableKeyword("_VERTEXCOLOR");

        // Set the render queue for proper transparency
        crystalMaterial.renderQueue = 3000; // Transparent queue

        // Assign the material to the renderer
        meshRenderer.material = crystalMaterial;

        // Set up shadows for better depth perception
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; // No shadows from transparent parts
        meshRenderer.receiveShadows = true;
    }

    private void SetupWireframeMaterial()
    {
        // Create material for wireframe
        Shader lineShader = Shader.Find("Particles/Standard Unlit");
        if (lineShader == null)
        {
            lineShader = Shader.Find("Sprites/Default");
        }

        if (lineShader == null)
        {
            lineShader = Shader.Find("Diffuse");
        }

        wireframeMaterial = new Material(lineShader);

        // Make the wireframe bright and visible
        wireframeMaterial.SetColor("_Color", wireframeColor);
        wireframeMaterial.SetColor("_TintColor", wireframeColor);

        if (wireframeMaterial.HasProperty("_EmissionColor"))
        {
            wireframeMaterial.EnableKeyword("_EMISSION");
            wireframeMaterial.SetColor("_EmissionColor", wireframeColor * wireframeEmissionIntensity);
        }

        // Enable alpha blending
        wireframeMaterial.SetFloat("_Mode", 3); // Transparent
        wireframeMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        wireframeMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        wireframeMaterial.EnableKeyword("_ALPHABLEND_ON");

        // Set render queue to render after the main crystal mesh
        wireframeMaterial.renderQueue = 3100;

        // Assign to renderer
        wireframeMeshRenderer.material = wireframeMaterial;
        wireframeMeshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    // Make the veins pulse with white light while wireframe edges remain visible
    void Update()
    {
        if (crystalMaterial != null)
        {
            // Pulsing white light for veins
            float veinPulseIntensity = 1.0f + Mathf.Sin(Time.time * 2.5f) * 0.3f;

            // Secondary pulse for more interesting effect
            float secondaryPulse = 1.0f + Mathf.Sin(Time.time * 1.2f + 1.0f) * 0.2f;
            float combinedPulse = veinPulseIntensity * secondaryPulse;

            // Update emission with glowing white
            if (crystalMaterial.HasProperty("_EmissionColor"))
            {
                // Pure white emission for veins
                Color veinEmission = new Color(
                    1.0f * combinedPulse,
                    1.0f * combinedPulse,
                    1.0f * combinedPulse,
                    1.0f
                );
                crystalMaterial.SetColor("_EmissionColor", veinEmission * 2.5f);
            }

            // Pulse wireframe color slightly
            if (wireframeMaterial != null)
            {
                float wireframePulse = 1.0f + Mathf.Sin(Time.time * 1.8f) * 0.1f;

                Color pulsingWireframe = new Color(
                    wireframeColor.r * wireframePulse,
                    wireframeColor.g * wireframePulse,
                    wireframeColor.b * wireframePulse,
                    wireframeColor.a
                );

                wireframeMaterial.SetColor("_Color", pulsingWireframe);
                wireframeMaterial.SetColor("_TintColor", pulsingWireframe);

                if (wireframeMaterial.HasProperty("_EmissionColor"))
                {
                    wireframeMaterial.SetColor("_EmissionColor", pulsingWireframe * wireframeEmissionIntensity);
                }
            }

            // Add a subtle bloom/halo effect by adjusting ambient intensity
            RenderSettings.ambientIntensity = 1.0f + (combinedPulse - 1.0f) * 0.2f;
        }
    }

#if UNITY_EDITOR
    // This helps to visualize in the editor
    void OnValidate()
    {
        if (Application.isPlaying) return;

        // Update the material in editor when properties change
        if (meshRenderer != null && meshRenderer.sharedMaterial != null)
        {
            if (meshRenderer.sharedMaterial.HasProperty("_BaseColor"))
            {
                meshRenderer.sharedMaterial.SetColor("_BaseColor", crystalColor);
                meshRenderer.sharedMaterial.SetColor("_EmissionColor", emissionColor * emissionIntensity);
            }
            else if (meshRenderer.sharedMaterial.HasProperty("_Color"))
            {
                meshRenderer.sharedMaterial.SetColor("_Color", crystalColor);
                if (meshRenderer.sharedMaterial.HasProperty("_EmissionColor"))
                {
                    meshRenderer.sharedMaterial.SetColor("_EmissionColor", emissionColor * emissionIntensity);
                }
            }
        }
    }
#endif
}