using UnityEngine;

[CreateAssetMenu(
    fileName = "TetrisConfig",
    menuName = "BackToSchool/Minigame/Tetris Config",
    order = 11)]
public class TetrisMinigameConfig : ScriptableObject
{
    [System.Serializable]
    public class ShapePrefabSlots
    {
        public GameObject I;
        public GameObject O;
        public GameObject T;
        public GameObject S;
        public GameObject Z;
        public GameObject J;
        public GameObject L;

        public GameObject Get(int shapeIdx)
        {
            return shapeIdx switch
            {
                0 => I,
                1 => O,
                2 => T,
                3 => S,
                4 => Z,
                5 => J,
                6 => L,
                _ => null
            };
        }

        public void Set(int shapeIdx, GameObject value)
        {
            switch (shapeIdx)
            {
                case 0: I = value; break;
                case 1: O = value; break;
                case 2: T = value; break;
                case 3: S = value; break;
                case 4: Z = value; break;
                case 5: J = value; break;
                case 6: L = value; break;
            }
        }

        public bool HasAny()
        {
            return I != null || O != null || T != null || S != null || Z != null || J != null || L != null;
        }
    }

    [System.Serializable]
    public class ShapeOffsetSlots
    {
        public Vector2 I;
        public Vector2 O;
        public Vector2 T;
        public Vector2 S;
        public Vector2 Z;
        public Vector2 J;
        public Vector2 L;

        public Vector2 Get(int shapeIdx)
        {
            return shapeIdx switch
            {
                0 => I,
                1 => O,
                2 => T,
                3 => S,
                4 => Z,
                5 => J,
                6 => L,
                _ => Vector2.zero
            };
        }

        public void Set(int shapeIdx, Vector2 value)
        {
            switch (shapeIdx)
            {
                case 0: I = value; break;
                case 1: O = value; break;
                case 2: T = value; break;
                case 3: S = value; break;
                case 4: Z = value; break;
                case 5: J = value; break;
                case 6: L = value; break;
            }
        }
    }

    [System.Serializable]
    public class ShapeRotationSlots
    {
        public float I;
        public float O;
        public float T;
        public float S;
        public float Z;
        public float J;
        public float L;

        public float Get(int shapeIdx)
        {
            return shapeIdx switch
            {
                0 => I,
                1 => O,
                2 => T,
                3 => S,
                4 => Z,
                5 => J,
                6 => L,
                _ => 0f
            };
        }

        public void Set(int shapeIdx, float value)
        {
            switch (shapeIdx)
            {
                case 0: I = value; break;
                case 1: O = value; break;
                case 2: T = value; break;
                case 3: S = value; break;
                case 4: Z = value; break;
                case 5: J = value; break;
                case 6: L = value; break;
            }
        }
    }

    [System.Serializable]
    public class DayBlockSet
    {
        [Range(1, 4)] public int day = 1;
        [Tooltip("Fallback block prefab for this day.")]
        public GameObject defaultBlockPrefab;

        [Header("Per-Shape Prefabs")]
        public ShapePrefabSlots shapePrefabsByName = new ShapePrefabSlots();

        [Header("Per-Shape Visual Offsets")]
        public ShapeOffsetSlots shapeVisualOffsetsByName = new ShapeOffsetSlots();

        [Header("Per-Shape Base Rotations")]
        public ShapeRotationSlots shapeBaseRotationsByName = new ShapeRotationSlots();

        [SerializeField, HideInInspector] private bool migratedFromLegacyArrays;

        // Legacy serialized fields kept for auto-migration.
        [SerializeField, HideInInspector] public GameObject[] shapePrefabs = new GameObject[7];
        [SerializeField, HideInInspector] public Vector2[] shapeVisualOffsets = new Vector2[7];
        [SerializeField, HideInInspector] public float[] shapeBaseRotations = new float[7];

        public GameObject GetShapePrefab(int shapeIdx)
        {
            MigrateLegacyIfNeeded();

            var named = shapePrefabsByName.Get(shapeIdx);
            if (named != null)
                return named;

            return null;
        }

        public Vector2 GetShapeOffset(int shapeIdx)
        {
            MigrateLegacyIfNeeded();
            return shapeVisualOffsetsByName.Get(shapeIdx);
        }

        public float GetShapeBaseRotation(int shapeIdx)
        {
            MigrateLegacyIfNeeded();
            return shapeBaseRotationsByName.Get(shapeIdx);
        }

        public void MigrateLegacyIfNeeded()
        {
            if (migratedFromLegacyArrays)
                return;

            bool hasLegacyPrefab = shapePrefabs != null && shapePrefabs.Length > 0;
            bool hasLegacyOffset = shapeVisualOffsets != null && shapeVisualOffsets.Length > 0;
            bool hasLegacyRotation = shapeBaseRotations != null && shapeBaseRotations.Length > 0;
            if (!hasLegacyPrefab && !hasLegacyOffset && !hasLegacyRotation)
            {
                migratedFromLegacyArrays = true;
                return;
            }

            for (int i = 0; i < 7; i++)
            {
                if (hasLegacyPrefab && i < shapePrefabs.Length && shapePrefabs[i] != null)
                    shapePrefabsByName.Set(i, shapePrefabs[i]);

                if (hasLegacyOffset && i < shapeVisualOffsets.Length)
                    shapeVisualOffsetsByName.Set(i, shapeVisualOffsets[i]);

                if (hasLegacyRotation && i < shapeBaseRotations.Length)
                    shapeBaseRotationsByName.Set(i, shapeBaseRotations[i]);
            }

            migratedFromLegacyArrays = true;
        }
    }

    [Header("Goal")]
    public int targetLockedPieces = 15;

    [Header("Difficulty")]
    public float fallInterval = 0.75f;
    public float softDropInterval = 0.06f;

    [Header("Flow")]
    public int penaltyOnFail = 1;

    [Header("Jelly Feel")]
    public bool enableJelly = true;
    [Range(0f, 0.35f)] public float fallStretchAmount = 0.08f;
    [Range(0f, 0.45f)] public float landSquashAmount = 0.16f;
    [Range(0f, 0.2f)] public float rotateJellyAmount = 0.06f;
    [Range(0.01f, 0.2f)] public float landLockDelay = 0.06f;
    [Range(1f, 40f)] public float jellySnapSpeed = 22f;
    [Range(1f, 40f)] public float jellyReturnSpeed = 14f;

    [Header("Board")]
    public int boardWidth = 9;
    public int boardHeight = 10;
    public float boardCellSize = 0.5f;
    public Vector2 boardOrigin = new Vector2(-2.5f, -2.5f);
    public bool autoFitGridToBoardSprite;
    public bool autoCenterGridToBoardSprite;
    public GameObject blockPrefab;
    [Tooltip("White/bright pixels are playable cells, black/transparent are blocked.")]
    public Texture2D boardMaskTexture;
    public bool useBoardMask;
    public TetrisBoard.MaskSampleMode boardMaskMode = TetrisBoard.MaskSampleMode.Luma;
    [Range(0f, 1f)] public float boardMaskThreshold = 0.5f;
    public bool invertBoardMask;
    public bool boardMaskFlipX;
    public bool boardMaskFlipY;
    public bool tintBlocksByShape = false;
    [Tooltip("If true, each tetromino uses one sprite/prefab object instead of 4 cell objects.")]
    public bool useCompositePieceVisuals = true;
    [Tooltip("Derive collision cells from composite sprite mesh (recommended for non-standard food-shaped pieces).")]
    public bool useSpriteDrivenCollision;
    [Tooltip("Apply extra pivot compensation for composite sprite pieces. Keep OFF for stable board alignment.")]
    public bool applyCompositePivotCompensation;
    [Tooltip("Auto-apply logical shape bounds center offset for composite visuals.")]
    public bool applyCompositeShapeCenterOffset = true;
    public DayBlockSet[] dayBlockSets = new DayBlockSet[0];

    private void OnValidate()
    {
        if (dayBlockSets == null)
            return;

        for (int i = 0; i < dayBlockSets.Length; i++)
        {
            var set = dayBlockSets[i];
            if (set == null)
                continue;

            set.MigrateLegacyIfNeeded();
        }
    }
}
