using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace KingdomTD.Flipbook.Tests
{
    public sealed class FlipbookGroundShadowTests
    {
        private Scene _scene;
        private FlipbookGroundShadowSettings _settings;
        private FlipbookRenderer _renderer;
        private FlipbookAnimationAsset _asset;
        private Material _material;
        private Texture2D _atlas;

        [SetUp]
        public void SetUp()
        {
            _scene = EditorSceneManager.NewPreviewScene();
            _settings = CreateObject("Settings").AddComponent<FlipbookGroundShadowSettings>();
            _settings.ShadowColor = Color.magenta;
            _settings.GroundHeight = 0.4f;
            _settings.Length = 1.2f;
            _settings.ScreenAngle = 60f;
            _atlas = new Texture2D(16, 8) { filterMode = FilterMode.Point };
            for (int x = 0; x < _atlas.width; x++)
            {
                for (int y = 0; y < _atlas.height; y++)
                {
                    _atlas.SetPixel(x, y, x < 4 || x >= 12 ? Color.white : Color.clear);
                }
            }
            _atlas.Apply();
            _material = new Material(Shader.Find("KingdomTD/Flipbook/World")) { enableInstancing = true };
            _asset = ScriptableObject.CreateInstance<FlipbookAnimationAsset>();
            using (SerializedObject asset = new SerializedObject(_asset))
            {
                asset.FindProperty("_mainTexture").objectReferenceValue = _atlas;
                asset.FindProperty("_worldMaterial").objectReferenceValue = _material;
                asset.FindProperty("_columns").intValue = 2;
                SerializedProperty clips = asset.FindProperty("_clips");
                clips.arraySize = 1;
                SerializedProperty clip = clips.GetArrayElementAtIndex(0);
                clip.FindPropertyRelative("AnimationName").stringValue = "Idle";
                clip.FindPropertyRelative("FrameCount").intValue = 2;
                clip.FindPropertyRelative("FrameRate").floatValue = 10f;
                clip.FindPropertyRelative("Speed").floatValue = 1f;
                SerializedProperty directions = asset.FindProperty("_directions");
                directions.arraySize = 2;
                directions.GetArrayElementAtIndex(0).FindPropertyRelative("SourceDirection").intValue = -1;
                SerializedProperty mirror = directions.GetArrayElementAtIndex(1);
                mirror.FindPropertyRelative("SourceDirection").intValue = 0;
                mirror.FindPropertyRelative("FlipX").boolValue = true;
                asset.ApplyModifiedPropertiesWithoutUndo();
            }
            _asset.SynchronizeWorldMaterial();
            _renderer = CreateObject("Character").AddComponent<FlipbookRenderer>();
            _renderer.SetAsset(_asset);
            _renderer.Play("Idle", false);
        }

        [TearDown]
        public void TearDown()
        {
            EditorSceneManager.ClosePreviewScene(_scene);
            Object.DestroyImmediate(_asset);
            Object.DestroyImmediate(_material);
            Object.DestroyImmediate(_atlas);
        }

        [Test]
        public void GroundShadowEnabled_SharesOneMeshAndMaterialAndPreservesPlayback()
        {
            MeshFilter filter = _renderer.GetComponent<MeshFilter>();
            Mesh original = filter.sharedMesh;
            Assert.AreEqual(4, original.vertexCount);
            _renderer.SetNormalizedTime(1f);
            _renderer.GroundShadowEnabled = true;
            Assert.AreEqual(8, filter.sharedMesh.vertexCount);
            Assert.AreEqual(1, filter.sharedMesh.subMeshCount);
            Assert.AreEqual(12, filter.sharedMesh.GetIndexCount(0));
            FlipbookRenderer another = CreateObject("Another character").AddComponent<FlipbookRenderer>();
            another.GroundShadowEnabled = true;
            another.SetAsset(_asset);
            Assert.AreSame(filter.sharedMesh, another.GetComponent<MeshFilter>().sharedMesh);
            Assert.AreSame(_material, another.TargetRenderer.sharedMaterial);
            _renderer.GroundShadowEnabled = false;
            Assert.AreSame(original, filter.sharedMesh);
            Assert.AreEqual(original.bounds, _renderer.TargetRenderer.localBounds);
            Assert.AreEqual(1, _renderer.CurrentFrame);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void GroundShadowToggleAndReenable_PreserveCurrentMaterial(bool supportsGroundShadow)
        {
            Shader shader = supportsGroundShadow ? _material.shader : Shader.Find("Universal Render Pipeline/Unlit");
            Material customMaterial = new Material(shader);
            try
            {
                MeshFilter filter = _renderer.GetComponent<MeshFilter>();
                _renderer.TargetRenderer.sharedMaterial = customMaterial;
                _renderer.GroundShadowEnabled = true;
                Assert.AreSame(customMaterial, _renderer.TargetRenderer.sharedMaterial);
                Assert.AreEqual(supportsGroundShadow ? 8 : 4, filter.sharedMesh.vertexCount);

                _renderer.enabled = false;
                Assert.AreEqual(4, filter.sharedMesh.vertexCount);
                _renderer.enabled = true;
                Assert.AreSame(customMaterial, _renderer.TargetRenderer.sharedMaterial);
                Assert.AreEqual(supportsGroundShadow ? 8 : 4, filter.sharedMesh.vertexCount);

                _renderer.GroundShadowEnabled = false;
                Assert.AreSame(customMaterial, _renderer.TargetRenderer.sharedMaterial);
                Assert.AreEqual(4, filter.sharedMesh.vertexCount);

                _renderer.enabled = false;
                _renderer.enabled = true;
                Assert.AreSame(customMaterial, _renderer.TargetRenderer.sharedMaterial);
            }
            finally
            {
                Object.DestroyImmediate(customMaterial);
            }
        }

        [UnityTest]
        public IEnumerator DisabledRenderer_AssigningAssetInInspectorRefreshesMaterialAndAtlas()
        {
            _renderer.enabled = false;
            Material replacementMaterial = new Material(_material);
            FlipbookAnimationAsset replacement = ScriptableObject.CreateInstance<FlipbookAnimationAsset>();
            try
            {
                using (SerializedObject asset = new SerializedObject(replacement))
                {
                    asset.FindProperty("_mainTexture").objectReferenceValue = _atlas;
                    asset.FindProperty("_worldMaterial").objectReferenceValue = replacementMaterial;
                    asset.FindProperty("_columns").intValue = 3;
                    asset.ApplyModifiedPropertiesWithoutUndo();
                }
                replacement.SynchronizeWorldMaterial();

                Undo.IncrementCurrentGroup();
                using (SerializedObject renderer = new SerializedObject(_renderer))
                {
                    renderer.FindProperty("_animationAsset").objectReferenceValue = replacement;
                    renderer.ApplyModifiedProperties();
                }
                Undo.FlushUndoRecordObjects();
                for (int i = 0; i < 10 && _renderer.TargetRenderer.sharedMaterial != replacementMaterial; i++)
                {
                    yield return null;
                }

                int columnsId = Shader.PropertyToID("_Columns");
                Assert.IsFalse(_renderer.enabled);
                Assert.IsTrue(_renderer.TargetRenderer.enabled);
                Assert.AreSame(replacementMaterial, _renderer.TargetRenderer.sharedMaterial);
                Assert.AreEqual(3f, _renderer.PropertyBlock.GetFloat(columnsId));

                Undo.PerformUndo();
                for (int i = 0; i < 10 && _renderer.TargetRenderer.sharedMaterial != _material; i++)
                {
                    yield return null;
                }

                Assert.AreSame(_material, _renderer.TargetRenderer.sharedMaterial);
                Assert.AreEqual(2f, _renderer.PropertyBlock.GetFloat(columnsId));
            }
            finally
            {
                Undo.ClearUndo(_renderer);
                Object.DestroyImmediate(replacement);
                Object.DestroyImmediate(replacementMaterial);
            }
        }

        [Test]
        public void GroundShadowSettings_KeepPreviewAndMainSceneProjectionsSeparate()
        {
            Scene main = SceneManager.GetActiveScene();
            GameObject first = CreateObject("First settings", main);
            GameObject second = CreateObject("Second settings", main);
            GameObject character = CreateObject("Main scene character", main);
            try
            {
                FlipbookGroundShadowSettings firstSettings = first.AddComponent<FlipbookGroundShadowSettings>();
                firstSettings.GroundHeight = 2f;
                FlipbookGroundShadowSettings secondSettings = second.AddComponent<FlipbookGroundShadowSettings>();
                secondSettings.GroundHeight = 4f;
                secondSettings.ShadowColor = Color.yellow;
                secondSettings.Length = 2f;

                FlipbookRenderer regular = character.AddComponent<FlipbookRenderer>();
                regular.SetAsset(_asset);
                regular.GroundShadowEnabled = true;
                _settings.enabled = false;
                _settings.enabled = true;
                _renderer.GroundShadowEnabled = true;
                regular.SendMessage("LateUpdate");

                int projectionId = Shader.PropertyToID("_FlipbookGroundProjection");
                int colorId = Shader.PropertyToID("_FlipbookGroundColor");
                Assert.AreEqual(Color.magenta, _renderer.PropertyBlock.GetColor(colorId));
                Assert.AreEqual(Color.yellow, regular.PropertyBlock.GetColor(colorId));
                Assert.AreEqual(0.401f, _renderer.PropertyBlock.GetVector(projectionId).x, 0.0001f);
                Assert.AreEqual(4.001f, regular.PropertyBlock.GetVector(projectionId).x, 0.0001f);
                Assert.GreaterOrEqual(regular.TargetRenderer.bounds.max.y, 4.001f);
                Assert.Less(_renderer.TargetRenderer.bounds.max.y, 2f);

                secondSettings.enabled = false;
                regular.SendMessage("LateUpdate");
                Assert.AreEqual(2.001f, regular.PropertyBlock.GetVector(projectionId).x, 0.0001f);
                Assert.Less(regular.TargetRenderer.bounds.max.y, 3f);
            }
            finally
            {
                Object.DestroyImmediate(character);
                Object.DestroyImmediate(second);
                Object.DestroyImmediate(first);
            }
        }

        [Test]
        public void GroundShadowBounds_ContainFootprintAfterJumpScaleAndHorizontalMovement()
        {
            Transform character = _renderer.transform;
            character.SetPositionAndRotation(new Vector3(30f, 5f, -15f), Quaternion.Euler(22f, 65f, 10f));
            character.localScale = new Vector3(2f, 3f, 0.5f);
            _renderer.GroundShadowFootHeight = 0.15f;
            _renderer.GroundShadowEnabled = true;
            Bounds original = _renderer.TargetRenderer.localBounds;
            character.position += new Vector3(-10f, 0f, 45f);
            _renderer.SendMessage("LateUpdate");
            Assert.AreEqual(original, _renderer.TargetRenderer.localBounds);

            character.position += Vector3.up * 6f;
            _settings.Length = 2f;
            _renderer.SendMessage("LateUpdate");
            Vector3 foot = character.TransformPoint(new Vector3(0f, -0.35f, 0f));
            foot.y = _settings.GroundHeight + 0.001f;
            for (int angle = 0; angle < 360; angle += 15)
            {
                Vector3 direction = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                Vector3 tip = foot + direction * (3f * 0.85f * _settings.Length) + Vector3.right;
                Assert.IsTrue(_renderer.TargetRenderer.bounds.Contains(tip), $"Angle {angle}");
            }
        }

        [Test]
        public void GroundShadowBounds_WithZeroDepthScale_FollowCharacterAndRestoreAutomaticBounds()
        {
            Transform character = _renderer.transform;
            character.position = new Vector3(-20f, 6f, 4f);
            character.localScale = new Vector3(1f, 2f, 0f);
            _renderer.GroundShadowEnabled = true;
            Vector3 tip = new Vector3(-20f, _settings.GroundHeight + 0.001f, 6f);
            Bounds bounds = _renderer.TargetRenderer.bounds;
            Assert.IsTrue(bounds.Contains(tip),
                $"Footprint {tip.ToString("F7")}, bounds {bounds.min.ToString("F7")} – {bounds.max.ToString("F7")}");
            character.position += Vector3.right * 50f;
            _renderer.SendMessage("LateUpdate");
            Assert.IsTrue(_renderer.TargetRenderer.bounds.Contains(tip + Vector3.right * 50f));
            _renderer.GroundShadowEnabled = false;
            character.position += Vector3.forward * 20f;
            Assert.AreEqual(character.position, _renderer.TargetRenderer.bounds.center);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Render_UsesMaterialColorsUntilEachParameterIsOverridden(bool groundShadowEnabled)
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null || GraphicsSettings.currentRenderPipeline == null)
            {
                Assert.Ignore("This render test requires a URP graphics device.");
            }

            Camera camera = CreateObject("Test camera").AddComponent<Camera>();
            camera.scene = _scene;
            camera.enabled = false;
            camera.orthographic = true;
            camera.orthographicSize = 1.5f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.transform.position = Vector3.back * 5f;

            RenderTexture target = RenderTexture.GetTemporary(128, 128, 24, RenderTextureFormat.ARGB32);
            Texture2D pixels = new Texture2D(target.width, target.height);
            Texture2D effect = new Texture2D(1, 1);
            RenderTexture previous = RenderTexture.active;
            try
            {
                effect.SetPixel(0, 0, Color.cyan);
                effect.Apply();
                _material.SetTexture("_EffectTex", effect);
                _material.SetColor("_ChangeColor", Color.red);
                _material.SetFloat("_ChangeRate", 1f);

                _renderer.Deinitialize();
                Assert.IsTrue(_renderer.Initialize());
                _renderer.transform.position = Vector3.left * 0.7f;
                _renderer.GroundShadowEnabled = groundShadowEnabled;
                _renderer.Play("Idle", false);
                _renderer.SetDirection(0);

                FlipbookRenderer other = CreateObject("Other character").AddComponent<FlipbookRenderer>();
                other.SetAsset(_asset);
                other.transform.position = Vector3.right * 0.7f;
                other.GroundShadowEnabled = groundShadowEnabled;
                other.Play("Idle", false);
                other.SetDirection(0);

                camera.targetTexture = target;
                Vector3 left = camera.WorldToScreenPoint(_renderer.transform.position + Vector3.left * 0.25f);
                Vector3 right = camera.WorldToScreenPoint(other.transform.position + Vector3.left * 0.25f);
                AssertCharacterColors(camera, pixels, left, right, Color.red, Color.red);

                _renderer.SetChangeColor(Color.blue);
                _material.SetColor("_ChangeColor", Color.green);
                AssertCharacterColors(camera, pixels, left, right, Color.blue, Color.green);

                _renderer.SetChangeRate(1f);
                _material.SetFloat("_ChangeRate", 0f);
                AssertCharacterColors(camera, pixels, left, right, Color.blue, Color.white);

                _renderer.SetChangeRate(0f);
                _renderer.SetEffectChangeRate(0f);
                _material.SetFloat("_EffectChangeRate", 1f);
                AssertCharacterColors(camera, pixels, left, right, Color.white, Color.cyan);
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
                Object.DestroyImmediate(pixels);
                Object.DestroyImmediate(effect);
            }
        }

        [TestCase(true, -2f, 0f, 45f)]
        [TestCase(true, -5.8f, 0f, 20f)]
        [TestCase(false, -2f, 0f, 60f)]
        [TestCase(false, 2f, 0f, 60f)]
        [TestCase(false, 2f, 65f, 60f)]
        [TestCase(true, -2f, 0f, 0f)]
        [TestCase(true, 2f, 0f, 180f)]
        [TestCase(false, -2f, 0f, 0f)]
        [TestCase(false, 2f, 65f, 180f)]
        public void Render_ProjectsCurrentFrameAndMirrorOntoGround(bool orthographic, float screenSide, float yaw,
            float screenAngle)
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null || GraphicsSettings.currentRenderPipeline == null)
            {
                Assert.Ignore("This render test requires a URP graphics device.");
            }

            Camera camera = CreateObject("Test camera").AddComponent<Camera>();
            camera.scene = _scene;
            camera.enabled = false;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.orthographic = orthographic;
            camera.orthographicSize = 3.5f;
            Quaternion orbit = Quaternion.Euler(0f, yaw, 0f);
            camera.transform.position = orbit * new Vector3(0f, 5.5f, -8f);
            camera.transform.LookAt(Vector3.up);
            _settings.ScreenAngle = screenAngle;
            bool onlyShadowInView = screenSide < -5f;
            if (onlyShadowInView)
            {
                _settings.Length = 3.5f;
            }
            _renderer.transform.SetPositionAndRotation(orbit * new Vector3(screenSide, 3.4f, 0f), camera.transform.rotation);
            _renderer.transform.localScale = new Vector3(1.8f, 2f, 1f);
            _renderer.GroundShadowFootHeight = 0.1f;
            _renderer.GroundShadowEnabled = true;
            _renderer.SetEffectChangeRate(1f);
            RenderTexture target = RenderTexture.GetTemporary(384, 320, 24, RenderTextureFormat.ARGB32);
            Texture2D pixels = new Texture2D(target.width, target.height);
            Material scenery = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { color = Color.cyan };
            RenderTexture previous = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                GameObject floor = CreateScenery(PrimitiveType.Plane, scenery);
                floor.transform.position = Vector3.up * _settings.GroundHeight;
                floor.transform.localScale = Vector3.one * 4f;
                if (onlyShadowInView)
                {
                    Vector3 bodyRight = _renderer.transform.TransformPoint(Vector3.right * 0.5f);
                    Assert.Less(camera.WorldToViewportPoint(bodyRight).x, 0f);
                }
                Vector3 foot = _renderer.transform.TransformPoint(new Vector3(0f, -0.4f, 0f));
                foot.y = _settings.GroundHeight + 0.001f;
                float angle = _settings.ScreenAngle * Mathf.Deg2Rad;
                Vector2 heading = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                Vector3 direction = GroundDirection(camera, foot, heading);
                Vector3 width = Vector3.Cross(Vector3.up, direction) * 1.8f;
                Vector3 middle = foot + direction * (2f * 0.55f * _settings.Length);
                Vector3 leftOnGround = middle - width * 0.25f;
                Vector3 left = camera.WorldToScreenPoint(leftOnGround);
                Vector3 right = camera.WorldToScreenPoint(middle + width * 0.25f);
                _renderer.SetDirection(0);
                AssertShadowPixels(camera, pixels, left, right, true, false);
                _renderer.SetNormalizedTime(1f);
                AssertShadowPixels(camera, pixels, left, right, false, true);
                _renderer.SetDirection(1);
                AssertShadowPixels(camera, pixels, left, right, true, false);
                GameObject obstacle = CreateScenery(PrimitiveType.Cube, scenery);
                obstacle.transform.position = leftOnGround - camera.ScreenPointToRay(left).direction * 0.4f;
                obstacle.transform.localScale = Vector3.one * 0.23f;
                AssertShadowPixels(camera, pixels, left, right, false, false);
                obstacle.SetActive(false);
                _renderer.GroundShadowEnabled = false;
                AssertShadowPixels(camera, pixels, left, right, false, false);
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
                Object.DestroyImmediate(pixels);
                Object.DestroyImmediate(scenery);
            }
        }

        private GameObject CreateScenery(PrimitiveType type, Material material)
        {
            GameObject obj = GameObject.CreatePrimitive(type);
            SceneManager.MoveGameObjectToScene(obj, _scene);
            obj.GetComponent<MeshRenderer>().sharedMaterial = material;
            return obj;
        }

        private GameObject CreateObject(string name)
        {
            return CreateObject(name, _scene);
        }

        private static GameObject CreateObject(string name, Scene scene)
        {
            GameObject obj = new GameObject(name);
            SceneManager.MoveGameObjectToScene(obj, scene);
            return obj;
        }

        private static Vector3 GroundDirection(Camera camera, Vector3 foot, Vector2 screenAxis)
        {
            Vector3 screen = camera.WorldToScreenPoint(foot) + (Vector3)(screenAxis * 2f);
            Ray ray = camera.ScreenPointToRay(screen);
            Assert.IsTrue(new Plane(Vector3.up, foot).Raycast(ray, out float distance));
            return (ray.GetPoint(distance) - foot).normalized;
        }

        private static void AssertShadowPixels(Camera camera, Texture2D pixels, Vector3 left, Vector3 right,
            bool expectedLeft, bool expectedRight)
        {
            ReadCameraPixels(camera, pixels);
            Color leftColor = pixels.GetPixel((int)left.x, (int)left.y);
            Color rightColor = pixels.GetPixel((int)right.x, (int)right.y);
            Assert.AreEqual(expectedLeft, IsShadow(leftColor), $"Left {left} = {leftColor}");
            Assert.AreEqual(expectedRight, IsShadow(rightColor), $"Right {right} = {rightColor}");
        }

        private static void AssertCharacterColors(Camera camera, Texture2D pixels, Vector3 left, Vector3 right,
            Color expectedLeft, Color expectedRight)
        {
            ReadCameraPixels(camera, pixels);
            Color leftColor = pixels.GetPixel((int)left.x, (int)left.y);
            Color rightColor = pixels.GetPixel((int)right.x, (int)right.y);
            Assert.Less(Vector4.Distance(expectedLeft, leftColor), 0.02f, $"Left expected {expectedLeft}, got {leftColor}");
            Assert.Less(Vector4.Distance(expectedRight, rightColor), 0.02f, $"Right expected {expectedRight}, got {rightColor}");
        }

        private static void ReadCameraPixels(Camera camera, Texture2D pixels)
        {
            camera.Render();
            RenderTexture.active = camera.targetTexture;
            pixels.ReadPixels(new Rect(0, 0, pixels.width, pixels.height), 0, 0);
        }

        private static bool IsShadow(Color pixel)
        {
            return pixel.r > 0.8f && pixel.g < 0.1f && pixel.b > 0.8f && pixel.a > 0.95f;
        }
    }
}
