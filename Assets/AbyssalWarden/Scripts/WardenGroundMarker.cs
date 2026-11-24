using UnityEngine;

namespace AbyssalWarden
{
    // Red danger circle drawn on the ground: a faint disc + bright rim, with an inner disc that fills up
    // until the attack lands. Built in code so it does not depend on any VFX pack.
    public class WardenGroundMarker : MonoBehaviour
    {
        const int Segments = 48;
        static Material sharedMaterial;
        static Mesh discMesh;

        Transform fill;
        float duration, timer, radius;

        public static WardenGroundMarker Create(Vector3 groundPoint, float radius, float duration, Color color)
        {
            var go = new GameObject("Warden Danger Circle");
            go.transform.position = groundPoint + Vector3.up * .08f;
            var marker = go.AddComponent<WardenGroundMarker>();
            marker.Build(radius, duration, color);
            return marker;
        }

        static Material SharedMaterial
        {
            get
            {
                if (sharedMaterial == null)
                {
                    // Sprites/Default: unlit, alpha-blended, tinted by vertex colour; always included in builds.
                    sharedMaterial = new Material(Shader.Find("Sprites/Default")) { name = "Warden Danger Circle" };
                    sharedMaterial.renderQueue = 3100;
                }
                return sharedMaterial;
            }
        }

        static Mesh DiscMesh
        {
            get
            {
                if (discMesh != null) return discMesh;
                var vertices = new Vector3[Segments + 1];
                var triangles = new int[Segments * 3];
                for (int i = 0; i < Segments; i++)
                {
                    float a = i * Mathf.PI * 2f / Segments;
                    vertices[i + 1] = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                    triangles[i * 3] = 0; triangles[i * 3 + 1] = (i + 1) % Segments + 1; triangles[i * 3 + 2] = i + 1;
                }
                discMesh = new Mesh { name = "Warden Disc", vertices = vertices, triangles = triangles };
                discMesh.RecalculateBounds();
                return discMesh;
            }
        }

        void Build(float radius, float time, Color color)
        {
            duration = Mathf.Max(.05f, time);
            this.radius = radius;
            AddDisc("Area", radius, new Color(color.r, color.g, color.b, .22f));
            fill = AddDisc("Fill", radius, new Color(color.r, color.g, color.b, .4f)).transform;
            fill.localScale = new Vector3(0, 1, 0);

            var rim = gameObject.AddComponent<LineRenderer>();
            rim.sharedMaterial = SharedMaterial;
            rim.loop = true; rim.useWorldSpace = false; rim.positionCount = Segments;
            rim.widthMultiplier = .12f;
            rim.startColor = rim.endColor = new Color(color.r, color.g, color.b, .95f);
            rim.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            for (int i = 0; i < Segments; i++)
            {
                float a = i * Mathf.PI * 2f / Segments;
                rim.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, .02f, Mathf.Sin(a) * radius));
            }
        }

        GameObject AddDisc(string name, float radius, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localScale = new Vector3(radius, 1, radius);
            var mesh = Instantiate(DiscMesh);
            var colors = new Color[mesh.vertexCount];
            for (int i = 0; i < colors.Length; i++) colors[i] = color;
            mesh.colors = colors;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = SharedMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return go;
        }

        void Update()
        {
            timer += Time.deltaTime;
            float k = Mathf.Clamp01(timer / duration);
            fill.localScale = new Vector3(k * radius, 1, k * radius);
        }

        void OnDestroy()
        {
            foreach (var filter in GetComponentsInChildren<MeshFilter>()) if (filter.sharedMesh != null) Destroy(filter.sharedMesh);
        }
    }
}
