using System;
using System.Collections.Generic;
using Game.Domain.World;
using UnityEngine;

namespace Game.Presentation
{
    public enum MapCellContent
    {
        Empty,
        PlayerBase,
        EnemyBase,
        NormalMine,
        GoldMine
    }

    public readonly struct MapCellSelection
    {
        public GridCoordinate Coordinate { get; }
        public MapCellContent Content { get; }
        public string DisplayName { get; }
        public string InteractionHint { get; }

        public MapCellSelection(
            GridCoordinate coordinate,
            MapCellContent content,
            string displayName,
            string interactionHint)
        {
            Coordinate = coordinate;
            Content = content;
            DisplayName = displayName ?? string.Empty;
            InteractionHint = interactionHint ?? string.Empty;
        }

        public override string ToString() =>
            $"{DisplayName} {Coordinate}\n{InteractionHint}";
    }

    /// <summary>
    /// 문명식 평면 월드 맵을 표시한다.
    /// 논리 좌표와 카메라는 가로로 래핑되고 세로는 극지 경계에서 막힌다.
    /// 화면 경계가 비지 않도록 동일한 맵 표면과 표식을 좌우에 한 장씩 복제한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StarterMapController : MonoBehaviour
    {
        private const int OpponentCount = 3;
        private const int SurfaceCopyRadius = 1;
        private const int PixelsPerTile = 8;

        [Header("문명식 대형 평면 맵")]
        [SerializeField, Range(40, 160)] private int mapWidth = 80;
        [SerializeField, Range(24, 100)] private int mapHeight = 48;
        [SerializeField, Range(20, 400)] private int mineCount = 160;
        [SerializeField, Min(0.5f)] private float tileSize = 1.15f;
        [SerializeField] private int playerStartX = 4;
        [SerializeField] private int playerStartY = 24;

        [Header("카메라")]
        [SerializeField, Min(1f)] private float cameraPanSpeed = 20f;
        [SerializeField, Min(1f)] private float minimumZoom = 7f;
        [SerializeField, Min(2f)] private float maximumZoom = 24f;
        [SerializeField, Min(1f)] private float startingZoom = 16f;
        [SerializeField] private bool createCameraIfMissing = true;
        [SerializeField] private bool createLightIfMissing = true;

        private static readonly Color PlayerStartColor =
            new Color(0.16f, 0.43f, 0.82f);
        private static readonly Color NormalMineColor =
            new Color(0.36f, 0.39f, 0.43f);
        private static readonly Color GoldMineColor =
            new Color(0.88f, 0.58f, 0.08f);
        private static readonly Color[] EnemyColors =
        {
            new Color(0.78f, 0.18f, 0.18f),
            new Color(0.72f, 0.27f, 0.68f),
            new Color(0.86f, 0.39f, 0.12f)
        };

        private readonly GridMapLayoutGenerator _layoutGenerator =
            new GridMapLayoutGenerator();
        private readonly HashSet<Collider> _mapSurfaceColliders =
            new HashSet<Collider>();
        private readonly List<Transform> _iconBillboards =
            new List<Transform>();

        private Transform _generatedRoot;
        private Camera _mapCamera;
        private Sprite _normalMineSprite;
        private Sprite _goldMineSprite;
        private Texture2D _mapTexture;
        private Material _mapMaterial;
        private Mesh _mapMesh;
        private Vector3 _cameraFocus;
        private int _generationSequence;

        public GridMapLayout CurrentLayout { get; private set; }
        public MapCellSelection? CurrentSelection { get; private set; }
        public event Action<MapCellSelection> CellSelected;

        public void Initialize()
        {
            EnsureLight();
            if (_generatedRoot == null)
                GenerateNewMap();
            EnsureCamera();
        }

        public void ResetMap()
        {
            RemoveGeneratedMap();
            EnsureLight();
            GenerateNewMap();
            EnsureCamera();
        }

        public void PanMap(float horizontal, float vertical)
        {
            if (CurrentLayout == null)
                return;

            _cameraFocus.x += horizontal;
            _cameraFocus.z += vertical;
            ClampAndWrapCameraFocus();
            ApplyCameraTransform();
        }

        public bool TryGetCoordinate(
            Vector3 worldPosition,
            out GridCoordinate coordinate)
        {
            coordinate = default;
            if (CurrentLayout == null)
                return false;

            float worldWidth = CurrentLayout.Width * tileSize;
            float worldHeight = CurrentLayout.Height * tileSize;
            float wrappedX = WrapCentered(worldPosition.x, worldWidth);
            float fromLeft = wrappedX + worldWidth * 0.5f;
            float fromBottom = worldPosition.z + worldHeight * 0.5f;
            int x = Mathf.FloorToInt(fromLeft / tileSize);
            int y = Mathf.FloorToInt(fromBottom / tileSize);

            if (y < 0 || y >= CurrentLayout.Height)
                return false;

            x = PositiveModulo(x, CurrentLayout.Width);
            coordinate = new GridCoordinate(x, y);
            return true;
        }

        private void GenerateNewMap()
        {
            int width = Mathf.Clamp(mapWidth, 40, 160);
            int height = Mathf.Clamp(mapHeight, 24, 100);
            int startX = PositiveModulo(playerStartX, width);
            int startY = Mathf.Clamp(playerStartY, 0, height - 1);
            var playerStart = new GridCoordinate(startX, startY);
            IReadOnlyList<GridCoordinate> opponentStarts =
                CreateOpponentStarts(width, height, playerStart);
            int seed = unchecked(
                Environment.TickCount ^
                GetInstanceID() ^
                (++_generationSequence * 397));
            int minimumMineCount = Mathf.RoundToInt(width * height * 0.03f);
            int requestedMines = Mathf.Max(mineCount, minimumMineCount);

            CurrentLayout = _layoutGenerator.Generate(
                width,
                height,
                requestedMines,
                seed,
                playerStart,
                opponentStarts,
                true);

            var rootObject = new GameObject(
                $"대형 평면 경제 월드_{width}x{height}");
            rootObject.transform.SetParent(transform, false);
            _generatedRoot = rootObject.transform;

            LoadMapIcons();
            BuildFlatMapCopies(CurrentLayout);
            BuildPlayerStart(CurrentLayout.PlayerStart);
            BuildOpponentStarts(CurrentLayout.OpponentStarts);
            BuildMines(CurrentLayout);
            FocusCameraOn(CurrentLayout.PlayerStart);
        }

        private static IReadOnlyList<GridCoordinate> CreateOpponentStarts(
            int width,
            int height,
            GridCoordinate playerStart)
        {
            var anchors = new[]
            {
                new GridCoordinate(
                    PositiveModulo(playerStart.X + width / 2, width),
                    height - 1 - playerStart.Y),
                new GridCoordinate(
                    PositiveModulo(playerStart.X + width / 3, width),
                    Mathf.Clamp(height * 2 / 3, 0, height - 1)),
                new GridCoordinate(
                    PositiveModulo(playerStart.X + width * 2 / 3, width),
                    Mathf.Clamp(height / 3, 0, height - 1))
            };

            var opponents = new List<GridCoordinate>(OpponentCount);
            for (int i = 0; i < anchors.Length; i++)
            {
                if (!anchors[i].Equals(playerStart) &&
                    !opponents.Contains(anchors[i]))
                {
                    opponents.Add(anchors[i]);
                }
            }

            return opponents;
        }

        private void BuildFlatMapCopies(GridMapLayout layout)
        {
            _mapTexture = CreateMapTexture(layout);
            _mapMesh = CreateMapMesh(layout);
            _mapMaterial = CreateMapMaterial(_mapTexture);
            float worldWidth = layout.Width * tileSize;
            float worldHeight = layout.Height * tileSize;

            for (int copy = -SurfaceCopyRadius;
                 copy <= SurfaceCopyRadius;
                 copy++)
            {
                var surface = new GameObject($"월드 표면 복제 {copy:+0;-0;0}");
                surface.transform.SetParent(_generatedRoot, false);
                surface.transform.localPosition =
                    new Vector3(copy * worldWidth, 0f, 0f);

                var filter = surface.AddComponent<MeshFilter>();
                filter.sharedMesh = _mapMesh;
                var renderer = surface.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = _mapMaterial;

                var collider = surface.AddComponent<BoxCollider>();
                collider.center = new Vector3(0f, -0.04f, 0f);
                collider.size = new Vector3(worldWidth, 0.08f, worldHeight);
                _mapSurfaceColliders.Add(collider);
            }
        }

        private Texture2D CreateMapTexture(GridMapLayout layout)
        {
            int textureWidth = layout.Width * PixelsPerTile;
            int textureHeight = layout.Height * PixelsPerTile;
            var texture = new Texture2D(
                textureWidth,
                textureHeight,
                TextureFormat.RGBA32,
                false,
                true)
            {
                name = $"월드 맵 텍스처_{layout.Width}x{layout.Height}",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color32[textureWidth * textureHeight];

            for (int tileY = 0; tileY < layout.Height; tileY++)
            {
                for (int tileX = 0; tileX < layout.Width; tileX++)
                {
                    var coordinate = new GridCoordinate(tileX, tileY);
                    Color baseColor = GetTerrainColor(
                        layout.GetTerrain(coordinate),
                        tileX,
                        tileY);
                    Color borderColor = Color.Lerp(
                        baseColor,
                        Color.black,
                        0.24f);

                    for (int pixelY = 0;
                         pixelY < PixelsPerTile;
                         pixelY++)
                    {
                        for (int pixelX = 0;
                             pixelX < PixelsPerTile;
                             pixelX++)
                        {
                            bool border = pixelX == 0 || pixelY == 0;
                            int x = tileX * PixelsPerTile + pixelX;
                            int y = tileY * PixelsPerTile + pixelY;
                            pixels[y * textureWidth + x] =
                                border ? borderColor : baseColor;
                        }
                    }
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private static Color GetTerrainColor(
            GridTerrainKind terrain,
            int x,
            int y)
        {
            float variation = ((x * 17 + y * 31) & 3) * 0.012f;
            Color color;
            switch (terrain)
            {
                case GridTerrainKind.Ocean:
                    color = new Color(0.055f, 0.20f, 0.35f);
                    break;
                case GridTerrainKind.Forest:
                    color = new Color(0.11f, 0.31f, 0.16f);
                    break;
                case GridTerrainKind.Desert:
                    color = new Color(0.66f, 0.53f, 0.27f);
                    break;
                case GridTerrainKind.Hills:
                    color = new Color(0.32f, 0.34f, 0.25f);
                    break;
                case GridTerrainKind.Tundra:
                    color = new Color(0.60f, 0.67f, 0.68f);
                    break;
                default:
                    color = new Color(0.25f, 0.46f, 0.23f);
                    break;
            }

            return new Color(
                Mathf.Clamp01(color.r + variation),
                Mathf.Clamp01(color.g + variation),
                Mathf.Clamp01(color.b + variation),
                1f);
        }

        private Mesh CreateMapMesh(GridMapLayout layout)
        {
            float halfWidth = layout.Width * tileSize * 0.5f;
            float halfHeight = layout.Height * tileSize * 0.5f;
            var mesh = new Mesh
            {
                name = "대형 평면 월드 메시"
            };
            mesh.vertices = new[]
            {
                new Vector3(-halfWidth, 0f, -halfHeight),
                new Vector3(-halfWidth, 0f, halfHeight),
                new Vector3(halfWidth, 0f, halfHeight),
                new Vector3(halfWidth, 0f, -halfHeight)
            };
            mesh.uv = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(1f, 0f)
            };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Material CreateMapMaterial(Texture texture)
        {
            Shader shader =
                Shader.Find("Universal Render Pipeline/Unlit") ??
                Shader.Find("Unlit/Texture") ??
                Shader.Find("Sprites/Default") ??
                Shader.Find("Standard");
            if (shader == null)
                throw new InvalidOperationException("맵 표시용 셰이더를 찾지 못했습니다.");

            var material = new Material(shader)
            {
                name = "대형 평면 월드 재질"
            };
            if (material.HasProperty("_BaseMap"))
                material.SetTexture("_BaseMap", texture);
            if (material.HasProperty("_MainTex"))
                material.SetTexture("_MainTex", texture);
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", Color.white);
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", Color.white);
            return material;
        }

        private static MapCellSelection DescribeCell(
            GridMapLayout layout,
            GridCoordinate coordinate)
        {
            if (coordinate.Equals(layout.PlayerStart))
            {
                return new MapCellSelection(
                    coordinate,
                    MapCellContent.PlayerBase,
                    "플레이어 본사",
                    "본사 방어, 건설, 창고 관리 명령을 연결할 수 있습니다.");
            }

            for (int i = 0; i < layout.OpponentStarts.Count; i++)
            {
                if (coordinate.Equals(layout.OpponentStarts[i]))
                {
                    return new MapCellSelection(
                        coordinate,
                        MapCellContent.EnemyBase,
                        $"경쟁 기업 {i + 1} 본사",
                        "정찰, 봉쇄, 공격 미션의 대상입니다.");
                }
            }

            for (int i = 0; i < layout.Mines.Count; i++)
            {
                MinePlacement mine = layout.Mines[i];
                if (!coordinate.Equals(mine.Coordinate))
                    continue;

                return mine.Kind == MineKind.Gold
                    ? new MapCellSelection(
                        coordinate,
                        MapCellContent.GoldMine,
                        "금광",
                        "점령 후 금을 채굴하거나 적의 수송로를 습격할 수 있습니다.")
                    : new MapCellSelection(
                        coordinate,
                        MapCellContent.NormalMine,
                        "일반 광산",
                        "점령 후 철·석탄 같은 산업 자원을 채굴할 수 있습니다.");
            }

            GridTerrainKind terrain = layout.GetTerrain(coordinate);
            return new MapCellSelection(
                coordinate,
                MapCellContent.Empty,
                GetTerrainName(terrain),
                terrain == GridTerrainKind.Ocean
                    ? "해상 운송과 해상 임무용 지역입니다. 지상 시설은 건설할 수 없습니다."
                    : "공장, 창고, 전초기지 건설 후보지입니다.");
        }

        private static string GetTerrainName(GridTerrainKind terrain)
        {
            switch (terrain)
            {
                case GridTerrainKind.Ocean: return "바다";
                case GridTerrainKind.Forest: return "숲";
                case GridTerrainKind.Desert: return "사막";
                case GridTerrainKind.Hills: return "구릉";
                case GridTerrainKind.Tundra: return "툰드라";
                default: return "평원";
            }
        }

        private void BuildPlayerStart(GridCoordinate coordinate)
        {
            ForEachSurfaceCopy(
                xOffset => CreateBlock(
                    $"플레이어 본사_{coordinate.X}_{coordinate.Y}",
                    ToWorldPosition(coordinate, xOffset) +
                    new Vector3(0f, 0.48f, 0f),
                    new Vector3(
                        tileSize * 0.76f,
                        0.82f,
                        tileSize * 0.76f),
                    PlayerStartColor,
                    _generatedRoot,
                    false));
        }

        private void BuildOpponentStarts(
            IReadOnlyList<GridCoordinate> opponentStarts)
        {
            for (int i = 0; i < opponentStarts.Count; i++)
            {
                int opponentIndex = i;
                GridCoordinate coordinate = opponentStarts[i];
                Color color = EnemyColors[i % EnemyColors.Length];
                ForEachSurfaceCopy(xOffset =>
                {
                    Vector3 position = ToWorldPosition(coordinate, xOffset);
                    CreateBlock(
                        $"경쟁 기업 {opponentIndex + 1} 본사_{coordinate.X}_{coordinate.Y}",
                        position + new Vector3(0f, 0.48f, 0f),
                        new Vector3(
                            tileSize * 0.76f,
                            0.82f,
                            tileSize * 0.76f),
                        color,
                        _generatedRoot,
                        false);
                    CreateBlock(
                        $"경쟁 기업 {opponentIndex + 1} 표식",
                        position + new Vector3(0f, 1.02f, 0f),
                        new Vector3(
                            tileSize * 0.34f,
                            0.28f,
                            tileSize * 0.34f),
                        color,
                        _generatedRoot,
                        false);
                });
            }
        }

        private void BuildMines(GridMapLayout layout)
        {
            for (int i = 0; i < layout.Mines.Count; i++)
            {
                MinePlacement mine = layout.Mines[i];
                bool isGold = mine.Kind == MineKind.Gold;
                string mineName = isGold ? "금광" : "일반 광산";
                Color mineColor = isGold ? GoldMineColor : NormalMineColor;
                ForEachSurfaceCopy(xOffset =>
                {
                    Vector3 position = ToWorldPosition(
                        mine.Coordinate,
                        xOffset);
                    CreateBlock(
                        $"{mineName}_{mine.Coordinate.X}_{mine.Coordinate.Y}",
                        position + new Vector3(0f, 0.22f, 0f),
                        new Vector3(
                            tileSize * 0.52f,
                            0.32f,
                            tileSize * 0.52f),
                        mineColor,
                        _generatedRoot,
                        false);
                    CreateMineIcon(
                        mineName + " 아이콘",
                        position + new Vector3(0f, 0.92f, 0f),
                        isGold ? _goldMineSprite : _normalMineSprite);
                });
            }
        }

        private void ForEachSurfaceCopy(Action<float> action)
        {
            float worldWidth = CurrentLayout.Width * tileSize;
            for (int copy = -SurfaceCopyRadius;
                 copy <= SurfaceCopyRadius;
                 copy++)
            {
                action(copy * worldWidth);
            }
        }

        private void LoadMapIcons()
        {
            if (_normalMineSprite == null)
            {
                _normalMineSprite = CreateRuntimeSprite(
                    Resources.Load<Texture2D>("MapIcons/mining_pickaxe"));
            }

            if (_goldMineSprite == null)
            {
                _goldMineSprite = CreateRuntimeSprite(
                    Resources.Load<Texture2D>("MapIcons/gold_coins"));
            }
        }

        private Sprite CreateRuntimeSprite(Texture2D texture)
        {
            if (texture == null)
                return null;

            float pixelsPerUnit = texture.width / (tileSize * 0.90f);
            return Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                pixelsPerUnit,
                0,
                SpriteMeshType.FullRect);
        }

        private void CreateMineIcon(
            string objectName,
            Vector3 position,
            Sprite sprite)
        {
            if (sprite == null)
                return;

            var iconObject = new GameObject(objectName);
            iconObject.transform.SetParent(_generatedRoot, false);
            iconObject.transform.position = position;
            var renderer = iconObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = 20;
            _iconBillboards.Add(iconObject.transform);
        }

        private Vector3 ToWorldPosition(
            GridCoordinate coordinate,
            float xOffset = 0f)
        {
            float widthOffset =
                (CurrentLayout.Width - 1) * tileSize * 0.5f;
            float heightOffset =
                (CurrentLayout.Height - 1) * tileSize * 0.5f;
            return new Vector3(
                coordinate.X * tileSize - widthOffset + xOffset,
                0f,
                coordinate.Y * tileSize - heightOffset);
        }

        private static GameObject CreateBlock(
            string objectName,
            Vector3 position,
            Vector3 scale,
            Color color,
            Transform parent,
            bool colliderEnabled)
        {
            var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = objectName;
            block.transform.SetParent(parent, false);
            block.transform.position = position;
            block.transform.localScale = scale;

            var properties = new MaterialPropertyBlock();
            properties.SetColor("_Color", color);
            properties.SetColor("_BaseColor", color);
            block.GetComponent<Renderer>().SetPropertyBlock(properties);
            Collider collider = block.GetComponent<Collider>();
            if (colliderEnabled)
            {
                collider.enabled = true;
            }
            else if (UnityEngine.Application.isPlaying)
            {
                UnityEngine.Object.Destroy(collider);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(collider);
            }
            return block;
        }

        private void Update()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            HandleCameraInput();
            HandleSelectionInput();
#endif
        }

#if ENABLE_LEGACY_INPUT_MANAGER
        private void HandleCameraInput()
        {
            if (_mapCamera == null || CurrentLayout == null)
                return;

            float horizontal = UnityEngine.Input.GetAxisRaw("Horizontal");
            float vertical = UnityEngine.Input.GetAxisRaw("Vertical");
            if (!Mathf.Approximately(horizontal, 0f) ||
                !Mathf.Approximately(vertical, 0f))
            {
                float zoomFactor =
                    _mapCamera.orthographicSize / Mathf.Max(1f, startingZoom);
                float distance =
                    cameraPanSpeed * zoomFactor * Time.unscaledDeltaTime;
                PanMap(horizontal * distance, vertical * distance);
            }

            float wheel = UnityEngine.Input.mouseScrollDelta.y;
            if (!Mathf.Approximately(wheel, 0f))
            {
                _mapCamera.orthographicSize = Mathf.Clamp(
                    _mapCamera.orthographicSize - wheel * 1.5f,
                    minimumZoom,
                    maximumZoom);
                ClampAndWrapCameraFocus();
                ApplyCameraTransform();
            }
        }

        private void HandleSelectionInput()
        {
            if (!UnityEngine.Input.GetMouseButtonDown(0))
                return;

            Camera camera = _mapCamera != null ? _mapCamera : Camera.main;
            if (camera == null)
                return;

            Ray ray = camera.ScreenPointToRay(UnityEngine.Input.mousePosition);
            if (!Physics.Raycast(ray, out RaycastHit hit, 2000f) ||
                !_mapSurfaceColliders.Contains(hit.collider) ||
                !TryGetCoordinate(hit.point, out GridCoordinate coordinate))
            {
                return;
            }

            MapCellSelection selection = DescribeCell(
                CurrentLayout,
                coordinate);
            CurrentSelection = selection;
            CellSelected?.Invoke(selection);
            Debug.Log($"지도 선택: {selection}");
        }
#endif

        private void LateUpdate()
        {
            Camera camera = _mapCamera != null ? _mapCamera : Camera.main;
            if (camera == null)
                return;

            Quaternion billboardRotation = camera.transform.rotation;
            for (int i = 0; i < _iconBillboards.Count; i++)
            {
                if (_iconBillboards[i] != null)
                    _iconBillboards[i].rotation = billboardRotation;
            }
        }

        private void FocusCameraOn(GridCoordinate coordinate)
        {
            _cameraFocus = ToWorldPosition(coordinate);
            ClampAndWrapCameraFocus();
            ApplyCameraTransform();
        }

        private void ClampAndWrapCameraFocus()
        {
            if (CurrentLayout == null)
                return;

            float worldWidth = CurrentLayout.Width * tileSize;
            float halfHeight = CurrentLayout.Height * tileSize * 0.5f;
            float zoom = _mapCamera != null
                ? _mapCamera.orthographicSize
                : startingZoom;
            float visibleGroundHalfHeight = zoom / Mathf.Sin(55f * Mathf.Deg2Rad);
            float verticalLimit = Mathf.Max(
                0f,
                halfHeight - visibleGroundHalfHeight);

            _cameraFocus.x = WrapCentered(_cameraFocus.x, worldWidth);
            _cameraFocus.z = Mathf.Clamp(
                _cameraFocus.z,
                -verticalLimit,
                verticalLimit);
        }

        private void ApplyCameraTransform()
        {
            if (_mapCamera == null)
                return;

            _mapCamera.transform.position =
                _cameraFocus + new Vector3(0f, 26f, -18.2f);
            _mapCamera.transform.rotation = Quaternion.Euler(55f, 0f, 0f);
        }

        private void RemoveGeneratedMap()
        {
            if (_generatedRoot != null)
            {
                GameObject generatedObject = _generatedRoot.gameObject;
                _generatedRoot = null;
                if (UnityEngine.Application.isPlaying)
                    Destroy(generatedObject);
                else
                    DestroyImmediate(generatedObject);
            }

            CurrentLayout = null;
            CurrentSelection = null;
            _mapSurfaceColliders.Clear();
            _iconBillboards.Clear();
            DestroyRuntimeAsset(_mapMaterial);
            DestroyRuntimeAsset(_mapTexture);
            DestroyRuntimeAsset(_mapMesh);
            _mapMaterial = null;
            _mapTexture = null;
            _mapMesh = null;
        }

        private void EnsureCamera()
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                if (!createCameraIfMissing)
                    return;

                var cameraObject = new GameObject("경제 월드 카메라");
                camera = cameraObject.AddComponent<Camera>();
                cameraObject.tag = "MainCamera";
            }

            _mapCamera = camera;
            camera.orthographic = true;
            camera.orthographicSize = Mathf.Clamp(
                startingZoom,
                minimumZoom,
                maximumZoom);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.025f, 0.035f, 0.055f);
            ClampAndWrapCameraFocus();
            ApplyCameraTransform();
        }

        private void EnsureLight()
        {
            if (!createLightIfMissing || FindAnyObjectByType<Light>() != null)
                return;

            var lightObject = new GameObject("경제 월드 조명");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.15f;
            light.color = new Color(1f, 0.93f, 0.82f);
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        private static float WrapCentered(float value, float length)
        {
            if (length <= 0f)
                return value;
            return Mathf.Repeat(value + length * 0.5f, length) -
                   length * 0.5f;
        }

        private static int PositiveModulo(int value, int divisor)
        {
            int remainder = value % divisor;
            return remainder < 0 ? remainder + divisor : remainder;
        }

        private static void DestroyRuntimeAsset(UnityEngine.Object asset)
        {
            if (asset == null)
                return;
            if (UnityEngine.Application.isPlaying)
                UnityEngine.Object.Destroy(asset);
            else
                UnityEngine.Object.DestroyImmediate(asset);
        }

        private void OnDestroy()
        {
            DestroyRuntimeAsset(_normalMineSprite);
            DestroyRuntimeAsset(_goldMineSprite);
            DestroyRuntimeAsset(_mapMaterial);
            DestroyRuntimeAsset(_mapTexture);
            DestroyRuntimeAsset(_mapMesh);
        }
    }
}
