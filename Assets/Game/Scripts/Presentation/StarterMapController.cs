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

    [DisallowMultipleComponent]
    public sealed class StarterMapController : MonoBehaviour
    {
        private const int GridSize = 15;
        private const int OpponentCount = 3;

        [Header("15x15 맵")]
        [SerializeField, Range(2, 80)] private int mineCount = 28;
        [SerializeField, Min(0.5f)] private float tileSize = 1.15f;
        [SerializeField] private int playerStartX = 0;
        [SerializeField] private int playerStartY = 0;
        [SerializeField] private bool createCameraIfMissing = true;
        [SerializeField] private bool createLightIfMissing = true;

        private static readonly Color NormalTileColor =
            new Color(0.10f, 0.16f, 0.20f);
        private static readonly Color AlternateTileColor =
            new Color(0.12f, 0.19f, 0.23f);
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
        private readonly Dictionary<Collider, MapCellSelection>
            _selectableCells =
                new Dictionary<Collider, MapCellSelection>();
        private readonly List<Transform> _iconBillboards =
            new List<Transform>();

        private Transform _generatedRoot;
        private Camera _mapCamera;
        private Sprite _normalMineSprite;
        private Sprite _goldMineSprite;
        private int _generationSequence;

        public GridMapLayout CurrentLayout { get; private set; }
        public MapCellSelection? CurrentSelection { get; private set; }
        public event Action<MapCellSelection> CellSelected;

        public void Initialize()
        {
            EnsureCamera();
            EnsureLight();
            if (_generatedRoot == null)
                GenerateNewMap();
        }

        public void ResetMap()
        {
            RemoveGeneratedMap();
            EnsureCamera();
            EnsureLight();
            GenerateNewMap();
        }

        private void GenerateNewMap()
        {
            int startX = Mathf.Clamp(playerStartX, 0, GridSize - 1);
            int startY = Mathf.Clamp(playerStartY, 0, GridSize - 1);
            var playerStart = new GridCoordinate(startX, startY);
            IReadOnlyList<GridCoordinate> opponentStarts =
                CreateOpponentStarts(playerStart);
            int seed = unchecked(
                Environment.TickCount ^
                GetInstanceID() ^
                (++_generationSequence * 397));

            CurrentLayout = _layoutGenerator.Generate(
                GridSize,
                mineCount,
                seed,
                playerStart,
                opponentStarts);

            var rootObject = new GameObject("15x15 경제 전장");
            rootObject.transform.SetParent(transform, false);
            _generatedRoot = rootObject.transform;

            LoadMapIcons();
            BuildTiles(CurrentLayout);
            BuildPlayerStart(CurrentLayout.PlayerStart);
            BuildOpponentStarts(CurrentLayout.OpponentStarts);
            BuildMines(CurrentLayout);
        }

        private static IReadOnlyList<GridCoordinate> CreateOpponentStarts(
            GridCoordinate playerStart)
        {
            var corners = new[]
            {
                new GridCoordinate(GridSize - 1, GridSize - 1),
                new GridCoordinate(0, GridSize - 1),
                new GridCoordinate(GridSize - 1, 0),
                new GridCoordinate(0, 0)
            };
            var opponents = new List<GridCoordinate>(OpponentCount);
            for (int i = 0;
                 i < corners.Length && opponents.Count < OpponentCount;
                 i++)
            {
                if (!corners[i].Equals(playerStart))
                    opponents.Add(corners[i]);
            }

            return opponents;
        }

        private void BuildTiles(GridMapLayout layout)
        {
            float offset = (layout.Size - 1) * tileSize * 0.5f;
            for (int y = 0; y < layout.Size; y++)
            {
                for (int x = 0; x < layout.Size; x++)
                {
                    var coordinate = new GridCoordinate(x, y);
                    bool isPlayerStart =
                        coordinate.Equals(layout.PlayerStart);
                    Color color = isPlayerStart
                        ? PlayerStartColor
                        : ((x + y) & 1) == 0
                            ? NormalTileColor
                            : AlternateTileColor;

                    GameObject tile = CreateBlock(
                        $"타일_{x}_{y}",
                        new Vector3(
                            x * tileSize - offset,
                            0f,
                            y * tileSize - offset),
                        new Vector3(
                            tileSize - 0.05f,
                            0.14f,
                            tileSize - 0.05f),
                        color,
                        _generatedRoot);
                    _selectableCells.Add(
                        tile.GetComponent<Collider>(),
                        DescribeCell(layout, coordinate));
                }
            }
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
                    "본사 방어, 건설, 창고 관리를 연결할 수 있습니다.");
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

                if (mine.Kind == MineKind.Gold)
                {
                    return new MapCellSelection(
                        coordinate,
                        MapCellContent.GoldMine,
                        "금광",
                        "점령 후 금을 채굴하거나 적의 운송로를 습격할 수 있습니다.");
                }

                return new MapCellSelection(
                    coordinate,
                    MapCellContent.NormalMine,
                    "일반 광산",
                    "점령 후 철·석탄 같은 산업 자원을 채굴할 수 있습니다.");
            }

            return new MapCellSelection(
                coordinate,
                MapCellContent.Empty,
                "빈 지역",
                "공장, 창고, 전초기지 건설 후보지입니다.");
        }

        private void BuildPlayerStart(GridCoordinate coordinate)
        {
            Vector3 position = ToWorldPosition(coordinate);
            CreateBlock(
                $"플레이어 본사_{coordinate.X}_{coordinate.Y}",
                position + new Vector3(0f, 0.48f, 0f),
                new Vector3(tileSize * 0.76f, 0.82f, tileSize * 0.76f),
                PlayerStartColor,
                _generatedRoot,
                false);
        }

        private void BuildOpponentStarts(
            IReadOnlyList<GridCoordinate> opponentStarts)
        {
            for (int i = 0; i < opponentStarts.Count; i++)
            {
                GridCoordinate coordinate = opponentStarts[i];
                Vector3 position = ToWorldPosition(coordinate);
                Color color = EnemyColors[i % EnemyColors.Length];
                CreateBlock(
                    $"경쟁 기업 {i + 1} 본사_{coordinate.X}_{coordinate.Y}",
                    position + new Vector3(0f, 0.48f, 0f),
                    new Vector3(
                        tileSize * 0.76f,
                        0.82f,
                        tileSize * 0.76f),
                    color,
                    _generatedRoot,
                    false);
                CreateBlock(
                    $"경쟁 기업 {i + 1} 표식",
                    position + new Vector3(0f, 1.02f, 0f),
                    new Vector3(
                        tileSize * 0.34f,
                        0.28f,
                        tileSize * 0.34f),
                    color,
                    _generatedRoot,
                    false);
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
                Vector3 position = ToWorldPosition(mine.Coordinate);

                CreateBlock(
                    $"{mineName}_{mine.Coordinate.X}_{mine.Coordinate.Y}",
                    position + new Vector3(0f, 0.25f, 0f),
                    new Vector3(
                        tileSize * 0.58f,
                        0.36f,
                        tileSize * 0.58f),
                    mineColor,
                    _generatedRoot,
                    false);
                CreateMineIcon(
                    mineName + " 아이콘",
                    position + new Vector3(0f, 1.02f, 0f),
                    isGold ? _goldMineSprite : _normalMineSprite);
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

        private Vector3 ToWorldPosition(GridCoordinate coordinate)
        {
            float offset = (GridSize - 1) * tileSize * 0.5f;
            return new Vector3(
                coordinate.X * tileSize - offset,
                0f,
                coordinate.Y * tileSize - offset);
        }

        private static GameObject CreateBlock(
            string objectName,
            Vector3 position,
            Vector3 scale,
            Color color,
            Transform parent,
            bool colliderEnabled = true)
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
            block.GetComponent<Collider>().enabled = colliderEnabled;
            return block;
        }

        private void Update()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            if (!UnityEngine.Input.GetMouseButtonDown(0))
                return;

            Camera camera = _mapCamera != null ? _mapCamera : Camera.main;
            if (camera == null)
                return;

            Ray ray = camera.ScreenPointToRay(UnityEngine.Input.mousePosition);
            if (!Physics.Raycast(ray, out RaycastHit hit, 1000f) ||
                !_selectableCells.TryGetValue(
                    hit.collider,
                    out MapCellSelection selection))
            {
                return;
            }

            CurrentSelection = selection;
            CellSelected?.Invoke(selection);
            Debug.Log($"지도 선택: {selection}");
#endif
        }

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

        private void RemoveGeneratedMap()
        {
            if (_generatedRoot == null)
                return;

            GameObject generatedObject = _generatedRoot.gameObject;
            _generatedRoot = null;
            CurrentLayout = null;
            CurrentSelection = null;
            _selectableCells.Clear();
            _iconBillboards.Clear();

            if (UnityEngine.Application.isPlaying)
                Destroy(generatedObject);
            else
                DestroyImmediate(generatedObject);
        }

        private void EnsureCamera()
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                if (!createCameraIfMissing)
                    return;

                var cameraObject = new GameObject("경제 전장 카메라");
                camera = cameraObject.AddComponent<Camera>();
                cameraObject.tag = "MainCamera";
            }

            _mapCamera = camera;
            camera.transform.position = new Vector3(0f, 24f, -21f);
            camera.transform.rotation = Quaternion.Euler(50f, 0f, 0f);
            camera.orthographic = true;
            camera.orthographicSize = 13.2f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.025f, 0.035f, 0.055f);
        }

        private void EnsureLight()
        {
            if (!createLightIfMissing || FindAnyObjectByType<Light>() != null)
                return;

            var lightObject = new GameObject("경제 전장 조명");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.15f;
            light.color = new Color(1f, 0.93f, 0.82f);
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        private void OnDestroy()
        {
            if (_normalMineSprite != null)
                Destroy(_normalMineSprite);
            if (_goldMineSprite != null)
                Destroy(_goldMineSprite);
        }
    }
}
