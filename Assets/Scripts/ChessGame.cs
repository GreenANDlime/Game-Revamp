using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

public class ChessGame : MonoBehaviour
{
    private class ChessMove
    {
        public ChessPiece piece;
        public Vector2Int from;
        public Vector2Int to;
        public bool isCapture;
        public ChessPiece capturedPiece;
        public bool isEnPassant;
        public ChessPiece enPassantCapturedPiece;
        public Vector2Int enPassantCapturedPos;
        public bool isCastle;
        public ChessPiece castleRook;
        public Vector2Int rookFrom;
        public Vector2Int rookTo;
        public bool isPromotion;
        public PieceType promotionType = PieceType.Queen;
        public bool isDoublePawnPush;
    }
    [Header("Tornado")]
    [Range(0f, 1f)] public float tornadoChance = 0.15f;
    public Sprite tornadoSprite;
    public float tornadoDisplayTime = 3f;
    public Vector3 tornadoScale = new Vector3(2f, 2f, 1f);

    [Header("Snowstorm")]
    [Range(0f, 1f)] public float snowstormChance = 0.2f;
    public Sprite iceBlockSprite;
    public Sprite snowflakeSprite;
    public Transform weatherParent;
    public Color iceOverlayColor = new Color(1f, 1f, 1f, 0.6f);
    public int minFrozenPieces = 3;
    public int maxFrozenPieces = 5;
    public float snowflakeSideX = 5.5f;
    public float snowflakeTopY = 3.5f;
    public float snowflakeSpacing = 1f;
    public Vector3 iceScale = new Vector3(1f, 1f, 1f);
    public Vector3 snowflakeScale = new Vector3(0.75f, 0.75f, 1f);

    [Header("Thunderstorm")]
    [Range(0f, 1f)] public float thunderstormChance = 0.3333333f;
    public Sprite thunderCloudSprite;
    public Transform stormParent;
    public float stormDisplayTime = 1.2f;

    [Header("Board")]
    public float tileSize = 1f;
    public Vector2 boardOrigin = new Vector2(-3.5f, -3.5f);

    [Header("Parents")]
    public Transform pieceParent;
    public Transform highlightParent;

    [Header("Prefab")]
    public GameObject piecePrefab;

    [Header("White Sprites")]
    public Sprite whitePawn;
    public Sprite whiteRook;
    public Sprite whiteKnight;
    public Sprite whiteBishop;
    public Sprite whiteQueen;
    public Sprite whiteKing;

    [Header("Black Sprites")]
    public Sprite blackPawn;
    public Sprite blackRook;
    public Sprite blackKnight;
    public Sprite blackBishop;
    public Sprite blackQueen;
    public Sprite blackKing;

    [Header("Highlight")]
    public Color highlightColor = new Color(0f, 1f, 0f, 0.35f);

    [Header("Defection Rule")]
    [Range(0f, 1f)] public float defectionChance = 0.2f;
    public bool kingsCanDefect = false;


    private enum WeatherType
    {
        None,
        Thunderstorm,
        Snowstorm,
        Tornado
    }

    private class FrozenPieceData
    {
        public ChessPiece piece;
        public GameObject iceOverlay;
        public int whiteTurnsRemaining = 2;
        public int blackTurnsRemaining = 2;
    }

    private WeatherType activeWeather = WeatherType.None;
    private List<FrozenPieceData> frozenPieces = new List<FrozenPieceData>();
    private List<GameObject> activeSnowflakes = new List<GameObject>();

    private GameObject activeTornado;

    private ChessPiece[,] board = new ChessPiece[8, 8];
    private ChessPiece selectedPiece;
    private PieceColor currentTurn = PieceColor.White;
    private GameObject activeStormCloud;

    private List<GameObject> highlights = new List<GameObject>();
    private Sprite cachedHighlightSprite;
    private List<ChessMove> currentLegalMoves = new List<ChessMove>();

    private bool gameOver;
    private string gameOverMessage = "";

    private bool whiteKingMoved;
    private bool blackKingMoved;
    private bool whiteLeftRookMoved;
    private bool whiteRightRookMoved;
    private bool blackLeftRookMoved;
    private bool blackRightRookMoved;

    private Vector2Int? enPassantTargetSquare;
    private ChessPiece enPassantPawn;

    private int halfmoveClock;
    private int fullmoveNumber = 1;
    private Dictionary<string, int> repetitionTable = new Dictionary<string, int>();

    void Start()
    {
        SetupBoard();
        RecordCurrentPosition();
    }

    void Update()
    {
        UpdateFrozenOverlayPositions();

        if (gameOver)
            return;

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            HandleMouseClick();
        }
    }

    void HandleMouseClick()
    {
        if (Camera.main == null || Mouse.current == null)
            return;

        Vector2 mouseScreen = Mouse.current.position.ReadValue();
        Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(mouseScreen);
        mouseWorld.z = 0f;

        Vector2Int clickedSquare = WorldToBoard(mouseWorld);

        if (!IsInsideBoard(clickedSquare))
            return;

        ChessPiece clickedPiece = board[clickedSquare.x, clickedSquare.y];

        if (selectedPiece == null)
        {
            if (clickedPiece != null && clickedPiece.pieceColor == currentTurn && !IsPieceFrozen(clickedPiece))
            {
                SelectPiece(clickedPiece);
            }
            return;
        }
        if (clickedPiece != null && clickedPiece.pieceColor == currentTurn && !IsPieceFrozen(clickedPiece))
        {
            SelectPiece(clickedPiece);
            return;
        }

        ChessMove chosenMove = null;
        for (int i = 0; i < currentLegalMoves.Count; i++)
        {
            if (currentLegalMoves[i].to == clickedSquare)
            {
                chosenMove = currentLegalMoves[i];
                break;
            }
        }

        if (chosenMove != null)
        {
            MakeRealMove(chosenMove);
            selectedPiece = null;
            currentLegalMoves.Clear();
            ClearHighlights();
            EvaluateGameStateAfterMove();
        }
        else
        {
            selectedPiece = null;
            currentLegalMoves.Clear();
            ClearHighlights();
        }
    }

    void TryTriggerSnowstorm()
    {
        if (IsAnyWeatherActive())
            return;

        if (Random.value > snowstormChance)
            return;

        StartSnowstorm();
    }

    void TryTriggerTornado()
    {
        if (IsAnyWeatherActive())
            return;

        if (Random.value > tornadoChance)
            return;

        StartCoroutine(TornadoRoutine());
    }
    void ShowTornado()
    {
        if (tornadoSprite == null)
            return;

        if (activeTornado != null)
            Destroy(activeTornado);

        activeTornado = new GameObject("Tornado");

        if (weatherParent != null)
            activeTornado.transform.SetParent(weatherParent);

        activeTornado.transform.position = BoardToWorld(new Vector2Int(3, 3)) + new Vector3(tileSize * 0.5f, tileSize * 0.5f, 0f);
        activeTornado.transform.localScale = tornadoScale;

        SpriteRenderer sr = activeTornado.AddComponent<SpriteRenderer>();
        sr.sprite = tornadoSprite;
        sr.sortingOrder = 40;
    }

    void HideTornado()
    {
        if (activeTornado != null)
        {
            Destroy(activeTornado);
            activeTornado = null;
        }
    }

    IEnumerator TornadoRoutine()
    {
        activeWeather = WeatherType.Tornado;

        ShowTornado();

        yield return new WaitForSeconds(tornadoDisplayTime);

        ShufflePiecesForTornado();

        HideTornado();
        activeWeather = WeatherType.None;
    }

    void ShufflePiecesForTornado()
    {
        List<ChessPiece> piecesToShuffle = new List<ChessPiece>();
        List<Vector2Int> availableTiles = new List<Vector2Int>();

        for (int x = 0; x < 8; x++)
        {
            for (int y = 0; y < 8; y++)
            {
                ChessPiece piece = board[x, y];

                if (piece != null && piece.pieceType != PieceType.King)
                {
                    piecesToShuffle.Add(piece);
                }
            }
        }

        for (int x = 0; x < 8; x++)
        {
            for (int y = 0; y < 8; y++)
            {
                ChessPiece piece = board[x, y];

                if (piece == null || piece.pieceType != PieceType.King)
                {
                    availableTiles.Add(new Vector2Int(x, y));
                }
            }
        }

        if (piecesToShuffle.Count == 0)
            return;

        Dictionary<ChessPiece, Vector2Int> originalPositions = new Dictionary<ChessPiece, Vector2Int>();
        for (int i = 0; i < piecesToShuffle.Count; i++)
        {
            originalPositions[piecesToShuffle[i]] = piecesToShuffle[i].boardPosition;
        }

        bool success = false;
        int maxAttempts = 200;

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            ResetShuffledPiecesToOriginalPositions(piecesToShuffle, originalPositions);
            ClearNonKingBoardSquares();

            List<Vector2Int> shuffledTiles = new List<Vector2Int>(availableTiles);
            ShuffleVector2IntList(shuffledTiles);

            for (int i = 0; i < piecesToShuffle.Count; i++)
            {
                ChessPiece piece = piecesToShuffle[i];
                Vector2Int newPos = shuffledTiles[i];

                piece.boardPosition = newPos;
                board[newPos.x, newPos.y] = piece;
            }

            if (!IsKingInCheck(PieceColor.White) && !IsKingInCheck(PieceColor.Black))
            {
                success = true;
                break;
            }
        }

        if (!success)
        {
            ResetShuffledPiecesToOriginalPositions(piecesToShuffle, originalPositions);
            ClearNonKingBoardSquares();

            for (int i = 0; i < piecesToShuffle.Count; i++)
            {
                ChessPiece piece = piecesToShuffle[i];
                Vector2Int originalPos = originalPositions[piece];

                piece.boardPosition = originalPos;
                board[originalPos.x, originalPos.y] = piece;
            }

            Debug.Log("Tornado could not find a valid no-check shuffle. Board restored.");
            return;
        }

        for (int i = 0; i < piecesToShuffle.Count; i++)
        {
            piecesToShuffle[i].transform.position = BoardToWorld(piecesToShuffle[i].boardPosition);
        }

        UpdateFrozenOverlayPositions();

        Debug.Log("Tornado shuffled the board.");
    }

    void ClearNonKingBoardSquares()
    {
        for (int x = 0; x < 8; x++)
        {
            for (int y = 0; y < 8; y++)
            {
                ChessPiece piece = board[x, y];

                if (piece != null && piece.pieceType != PieceType.King)
                {
                    board[x, y] = null;
                }
            }
        }
    }

    void ResetShuffledPiecesToOriginalPositions(List<ChessPiece> pieces, Dictionary<ChessPiece, Vector2Int> originalPositions)
    {
        for (int i = 0; i < pieces.Count; i++)
        {
            ChessPiece piece = pieces[i];
            piece.boardPosition = originalPositions[piece];
        }
    }

    void ShuffleVector2IntList(List<Vector2Int> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            Vector2Int temp = list[i];
            list[i] = list[j];
            list[j] = temp;
        }
    }
    
    void TryTriggerThunderstorm()
    {
        if (IsAnyWeatherActive())
            return;

        if (Random.value > thunderstormChance)
            return;

        StartCoroutine(ThunderstormRoutine());
    }

    void StartSnowstorm()
    {
        activeWeather = WeatherType.Snowstorm;

        ShowSnowflakes();

        List<ChessPiece> candidates = new List<ChessPiece>();

        for (int x = 0; x < 8; x++)
        {
            for (int y = 0; y < 8; y++)
            {
                ChessPiece piece = board[x, y];

                if (piece != null &&
                    piece.pieceType != PieceType.King &&
                    !IsPieceFrozen(piece))
                {
                    candidates.Add(piece);
                }
            }
        }

        if (candidates.Count == 0)
        {
            EndSnowstorm();
            return;
        }

        int amountToFreeze = Random.Range(minFrozenPieces, maxFrozenPieces + 1);
        amountToFreeze = Mathf.Min(amountToFreeze, candidates.Count);

        for (int i = 0; i < amountToFreeze; i++)
        {
            int randomIndex = Random.Range(0, candidates.Count);
            ChessPiece chosenPiece = candidates[randomIndex];
            candidates.RemoveAt(randomIndex);

            FreezePiece(chosenPiece);
        }

        Debug.Log("Snowstorm started. Frozen pieces: " + amountToFreeze);
    }

    void FreezePiece(ChessPiece piece)
    {
        if (piece == null || iceBlockSprite == null)
            return;

        GameObject iceObj = new GameObject("IceBlock");

        if (weatherParent != null)
            iceObj.transform.SetParent(weatherParent);

        iceObj.transform.position = piece.transform.position;

        SpriteRenderer sr = iceObj.AddComponent<SpriteRenderer>();
        sr.sprite = iceBlockSprite;
        sr.color = iceOverlayColor;
        sr.sortingOrder = 20;

        // 🔥 MATCH SIZE TO PIECE
        SpriteRenderer pieceSR = piece.GetComponent<SpriteRenderer>();

        if (pieceSR != null && pieceSR.sprite != null)
        {
            Vector2 pieceSize = pieceSR.bounds.size;
            Vector2 iceSize = sr.bounds.size;

            if (iceSize.x > 0f && iceSize.y > 0f)
            {
                float scaleX = pieceSize.x / iceSize.x;
                float scaleY = pieceSize.y / iceSize.y;

                iceObj.transform.localScale = new Vector3(scaleX, scaleY, 1f);
            }
        }

        // 🔥 OPTIONAL: make it slightly bigger than the piece
        iceObj.transform.localScale *= 1.15f;

        FrozenPieceData data = new FrozenPieceData();
        data.piece = piece;
        data.iceOverlay = iceObj;

        frozenPieces.Add(data);
    }

    void ShowSnowflakes()
    {
        ClearSnowflakes();

        if (snowflakeSprite == null)
            return;

        for (int i = 0; i < 5; i++)
        {
            GameObject snowObj = new GameObject("Snowflake");

            if (weatherParent != null)
                snowObj.transform.SetParent(weatherParent);

            snowObj.transform.position = new Vector3(
                snowflakeSideX,
                snowflakeTopY - i * snowflakeSpacing,
                0f
            );

            snowObj.transform.localScale = snowflakeScale;

            SpriteRenderer sr = snowObj.AddComponent<SpriteRenderer>();
            sr.sprite = snowflakeSprite;
            sr.sortingOrder = 30;

            activeSnowflakes.Add(snowObj);
        }
    }

    void ClearSnowflakes()
    {
        for (int i = 0; i < activeSnowflakes.Count; i++)
        {
            if (activeSnowflakes[i] != null)
                Destroy(activeSnowflakes[i]);
        }

        activeSnowflakes.Clear();
    }

    void AdvanceFrozenPieceTimers(PieceColor turnThatJustStarted)
    {
        for (int i = frozenPieces.Count - 1; i >= 0; i--)
        {
            FrozenPieceData data = frozenPieces[i];

            if (data.piece == null)
            {
                if (data.iceOverlay != null)
                    Destroy(data.iceOverlay);

                frozenPieces.RemoveAt(i);
                continue;
            }

            if (turnThatJustStarted == PieceColor.White)
                data.whiteTurnsRemaining--;
            else
                data.blackTurnsRemaining--;

            if (data.whiteTurnsRemaining <= 0 && data.blackTurnsRemaining <= 0)
            {
                if (data.iceOverlay != null)
                    Destroy(data.iceOverlay);

                frozenPieces.RemoveAt(i);
            }
        }

        if (activeWeather == WeatherType.Snowstorm && frozenPieces.Count == 0)
        {
            EndSnowstorm();
        }
    }

    void EndSnowstorm()
    {
        for (int i = 0; i < frozenPieces.Count; i++)
        {
            if (frozenPieces[i].iceOverlay != null)
                Destroy(frozenPieces[i].iceOverlay);
        }

        frozenPieces.Clear();
        ClearSnowflakes();
        activeWeather = WeatherType.None;

        Debug.Log("Snowstorm ended.");
    }

    IEnumerator ThunderstormRoutine()
    {
        activeWeather = WeatherType.Thunderstorm;

        Vector2Int randomTile = new Vector2Int(Random.Range(0, 8), Random.Range(0, 8));
        Vector3 strikeWorldPos = BoardToWorld(randomTile);

        ShowStormCloud(strikeWorldPos);

        yield return new WaitForSeconds(stormDisplayTime * 0.5f);

        ChessPiece targetPiece = board[randomTile.x, randomTile.y];

        if (targetPiece != null && targetPiece.pieceType != PieceType.King)
        {
            RemoveFrozenStateFromPiece(targetPiece);

            board[randomTile.x, randomTile.y] = null;
            Destroy(targetPiece.gameObject);
            Debug.Log("Thunder struck " + randomTile + " and destroyed " + targetPiece.pieceColor + " " + targetPiece.pieceType);
        }
        else
        {
            Debug.Log("Thunder struck " + randomTile + " but nothing was destroyed.");
        }

        yield return new WaitForSeconds(stormDisplayTime * 0.5f);

        HideStormCloud();
        activeWeather = WeatherType.None;
    }

    void RemoveFrozenStateFromPiece(ChessPiece piece)
    {
        for (int i = frozenPieces.Count - 1; i >= 0; i--)
        {
            if (frozenPieces[i].piece == piece)
            {
                if (frozenPieces[i].iceOverlay != null)
                    Destroy(frozenPieces[i].iceOverlay);

                frozenPieces.RemoveAt(i);
            }
        }

        if (activeWeather == WeatherType.Snowstorm && frozenPieces.Count == 0)
        {
            EndSnowstorm();
        }
    }

    bool IsAnyWeatherActive()
    {
        return activeWeather != WeatherType.None;
    }

    bool IsPieceFrozen(ChessPiece piece)
    {
        for (int i = 0; i < frozenPieces.Count; i++)
        {
            if (frozenPieces[i].piece == piece)
                return true;
        }

        return false;
    }

    FrozenPieceData GetFrozenData(ChessPiece piece)
    {
        for (int i = 0; i < frozenPieces.Count; i++)
        {
            if (frozenPieces[i].piece == piece)
                return frozenPieces[i];
        }

        return null;
    }

    void ShowStormCloud(Vector3 worldPosition)
    {
        if (thunderCloudSprite == null)
            return;

        if (activeStormCloud != null)
            Destroy(activeStormCloud);

        activeStormCloud = new GameObject("ThunderCloud");

        if (stormParent != null)
            activeStormCloud.transform.SetParent(stormParent);

        activeStormCloud.transform.position = new Vector3(worldPosition.x, worldPosition.y, 0f);

        activeStormCloud.transform.localScale = new Vector3(1.5f, 1.5f, 1f);

        SpriteRenderer sr = activeStormCloud.AddComponent<SpriteRenderer>();
        sr.sprite = thunderCloudSprite;
        sr.sortingOrder = 50;
    }

    void HideStormCloud()
    {
        if (activeStormCloud != null)
        {
            Destroy(activeStormCloud);
            activeStormCloud = null;
        }
    }

    bool ShouldDefect(ChessMove move)
    {
        ChessPiece target = move.isEnPassant ? move.enPassantCapturedPiece : move.capturedPiece;

        if (target == null)
            return false;

        if (target.pieceType == PieceType.King && !kingsCanDefect)
            return false;

        return Random.value < defectionChance;
    }

    void ConvertPieceToColor(ChessPiece piece, PieceColor newColor)
    {
        piece.pieceColor = newColor;

        SpriteRenderer sr = piece.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            sr.sprite = GetSprite(piece.pieceType, piece.pieceColor);
        }
    }

    void SelectPiece(ChessPiece piece)
    {
        selectedPiece = piece;
        currentLegalMoves = GetLegalMoves(piece);
        ShowHighlights(currentLegalMoves);
    }

    void SetupBoard()
    {
        SpawnBackRow(PieceColor.White, 0);
        SpawnPawns(PieceColor.White, 1);

        SpawnPawns(PieceColor.Black, 6);
        SpawnBackRow(PieceColor.Black, 7);
    }

    void SpawnPawns(PieceColor color, int y)
    {
        for (int x = 0; x < 8; x++)
        {
            SpawnPiece(PieceType.Pawn, color, new Vector2Int(x, y));
        }
    }

    void SpawnBackRow(PieceColor color, int y)
    {
        SpawnPiece(PieceType.Rook, color, new Vector2Int(0, y));
        SpawnPiece(PieceType.Knight, color, new Vector2Int(1, y));
        SpawnPiece(PieceType.Bishop, color, new Vector2Int(2, y));
        SpawnPiece(PieceType.Queen, color, new Vector2Int(3, y));
        SpawnPiece(PieceType.King, color, new Vector2Int(4, y));
        SpawnPiece(PieceType.Bishop, color, new Vector2Int(5, y));
        SpawnPiece(PieceType.Knight, color, new Vector2Int(6, y));
        SpawnPiece(PieceType.Rook, color, new Vector2Int(7, y));
    }

    void SpawnPiece(PieceType type, PieceColor color, Vector2Int pos)
    {
        GameObject pieceObj = Instantiate(piecePrefab, pieceParent);
        pieceObj.name = color + "_" + type;

        SpriteRenderer sr = pieceObj.GetComponent<SpriteRenderer>();
        ChessPiece cp = pieceObj.GetComponent<ChessPiece>();

        if (sr == null)
            sr = pieceObj.AddComponent<SpriteRenderer>();

        if (cp == null)
            cp = pieceObj.AddComponent<ChessPiece>();

        sr.sprite = GetSprite(type, color);
        sr.sortingOrder = 10;

        cp.pieceType = type;
        cp.pieceColor = color;
        cp.boardPosition = pos;

        pieceObj.transform.position = BoardToWorld(pos);

        board[pos.x, pos.y] = cp;
    }

    Sprite GetSprite(PieceType type, PieceColor color)
    {
        if (color == PieceColor.White)
        {
            switch (type)
            {
                case PieceType.Pawn: return whitePawn;
                case PieceType.Rook: return whiteRook;
                case PieceType.Knight: return whiteKnight;
                case PieceType.Bishop: return whiteBishop;
                case PieceType.Queen: return whiteQueen;
                case PieceType.King: return whiteKing;
            }
        }
        else
        {
            switch (type)
            {
                case PieceType.Pawn: return blackPawn;
                case PieceType.Rook: return blackRook;
                case PieceType.Knight: return blackKnight;
                case PieceType.Bishop: return blackBishop;
                case PieceType.Queen: return blackQueen;
                case PieceType.King: return blackKing;
            }
        }

        return null;
    }

    List<ChessMove> GetLegalMoves(ChessPiece piece)
    {
        if (IsPieceFrozen(piece))
            return new List<ChessMove>();    

        List<ChessMove> pseudoMoves = GeneratePseudoLegalMoves(piece);
        List<ChessMove> legalMoves = new List<ChessMove>();

        for (int i = 0; i < pseudoMoves.Count; i++)
        {
            if (IsMoveLegal(piece, pseudoMoves[i]))
            {
                legalMoves.Add(pseudoMoves[i]);
            }
        }

        return legalMoves;
    }

    List<ChessMove> GenerateAllLegalMovesForColor(PieceColor color)
    {
        List<ChessMove> legalMoves = new List<ChessMove>();

        for (int x = 0; x < 8; x++)
        {
            for (int y = 0; y < 8; y++)
            {
                ChessPiece piece = board[x, y];
                if (piece != null && piece.pieceColor == color)
                {
                    legalMoves.AddRange(GetLegalMoves(piece));
                }
            }
        }

        return legalMoves;
    }

    List<ChessMove> GeneratePseudoLegalMoves(ChessPiece piece)
    {
        List<ChessMove> moves = new List<ChessMove>();

        switch (piece.pieceType)
        {
            case PieceType.Pawn:
                AddPawnMoves(piece, moves);
                break;
            case PieceType.Rook:
                AddSlidingMoves(piece, moves, new Vector2Int[]
                {
                    Vector2Int.up,
                    Vector2Int.down,
                    Vector2Int.left,
                    Vector2Int.right
                });
                break;
            case PieceType.Bishop:
                AddSlidingMoves(piece, moves, new Vector2Int[]
                {
                    new Vector2Int(1, 1),
                    new Vector2Int(1, -1),
                    new Vector2Int(-1, 1),
                    new Vector2Int(-1, -1)
                });
                break;
            case PieceType.Queen:
                AddSlidingMoves(piece, moves, new Vector2Int[]
                {
                    Vector2Int.up,
                    Vector2Int.down,
                    Vector2Int.left,
                    Vector2Int.right,
                    new Vector2Int(1, 1),
                    new Vector2Int(1, -1),
                    new Vector2Int(-1, 1),
                    new Vector2Int(-1, -1)
                });
                break;
            case PieceType.Knight:
                AddKnightMoves(piece, moves);
                break;
            case PieceType.King:
                AddKingMoves(piece, moves);
                break;
        }

        return moves;
    }

    void UpdateFrozenOverlayPositions()
    {
        for (int i = 0; i < frozenPieces.Count; i++)
        {
            if (frozenPieces[i].piece != null && frozenPieces[i].iceOverlay != null)
            {
                frozenPieces[i].iceOverlay.transform.position = frozenPieces[i].piece.transform.position;
            }
        }
    }

    void AddPawnMoves(ChessPiece piece, List<ChessMove> moves)
    {
        int direction = piece.pieceColor == PieceColor.White ? 1 : -1;
        int startRow = piece.pieceColor == PieceColor.White ? 1 : 6;
        int promotionRow = piece.pieceColor == PieceColor.White ? 7 : 0;

        Vector2Int oneForward = new Vector2Int(piece.boardPosition.x, piece.boardPosition.y + direction);
        if (IsInsideBoard(oneForward) && board[oneForward.x, oneForward.y] == null)
        {
            ChessMove move = CreateBasicMove(piece, oneForward);
            if (oneForward.y == promotionRow)
            {
                move.isPromotion = true;
                move.promotionType = PieceType.Queen;
            }
            moves.Add(move);

            Vector2Int twoForward = new Vector2Int(piece.boardPosition.x, piece.boardPosition.y + direction * 2);
            if (piece.boardPosition.y == startRow && board[twoForward.x, twoForward.y] == null)
            {
                ChessMove doubleMove = CreateBasicMove(piece, twoForward);
                doubleMove.isDoublePawnPush = true;
                moves.Add(doubleMove);
            }
        }

        Vector2Int diagLeft = new Vector2Int(piece.boardPosition.x - 1, piece.boardPosition.y + direction);
        Vector2Int diagRight = new Vector2Int(piece.boardPosition.x + 1, piece.boardPosition.y + direction);

        AddPawnCaptureMove(piece, diagLeft, promotionRow, moves);
        AddPawnCaptureMove(piece, diagRight, promotionRow, moves);

        if (enPassantTargetSquare.HasValue)
        {
            Vector2Int target = enPassantTargetSquare.Value;

            if (target.y == piece.boardPosition.y + direction && Mathf.Abs(target.x - piece.boardPosition.x) == 1)
            {
                Vector2Int capturedPos = new Vector2Int(target.x, piece.boardPosition.y);
                ChessPiece capturedPawn = board[capturedPos.x, capturedPos.y];

                if (capturedPawn != null &&
                    capturedPawn == enPassantPawn &&
                    capturedPawn.pieceType == PieceType.Pawn &&
                    capturedPawn.pieceColor != piece.pieceColor)
                {
                    ChessMove epMove = CreateBasicMove(piece, target);
                    epMove.isEnPassant = true;
                    epMove.isCapture = true;
                    epMove.enPassantCapturedPiece = capturedPawn;
                    epMove.enPassantCapturedPos = capturedPos;
                    moves.Add(epMove);
                }
            }
        }
    }

    void AddPawnCaptureMove(ChessPiece piece, Vector2Int target, int promotionRow, List<ChessMove> moves)
    {
        if (!IsInsideBoard(target))
            return;

        ChessPiece targetPiece = board[target.x, target.y];
        if (targetPiece != null && targetPiece.pieceColor != piece.pieceColor)
        {
            ChessMove move = CreateBasicMove(piece, target);
            move.isCapture = true;
            move.capturedPiece = targetPiece;

            if (target.y == promotionRow)
            {
                move.isPromotion = true;
                move.promotionType = PieceType.Queen;
            }

            moves.Add(move);
        }
    }

    void AddSlidingMoves(ChessPiece piece, List<ChessMove> moves, Vector2Int[] directions)
    {
        for (int i = 0; i < directions.Length; i++)
        {
            Vector2Int current = piece.boardPosition + directions[i];

            while (IsInsideBoard(current))
            {
                ChessPiece targetPiece = board[current.x, current.y];

                if (targetPiece == null)
                {
                    moves.Add(CreateBasicMove(piece, current));
                }
                else
                {
                    if (targetPiece.pieceColor != piece.pieceColor)
                    {
                        ChessMove move = CreateBasicMove(piece, current);
                        move.isCapture = true;
                        move.capturedPiece = targetPiece;
                        moves.Add(move);
                    }
                    break;
                }

                current += directions[i];
            }
        }
    }

    void AddKnightMoves(ChessPiece piece, List<ChessMove> moves)
    {
        Vector2Int[] offsets = new Vector2Int[]
        {
            new Vector2Int(1, 2),
            new Vector2Int(2, 1),
            new Vector2Int(2, -1),
            new Vector2Int(1, -2),
            new Vector2Int(-1, -2),
            new Vector2Int(-2, -1),
            new Vector2Int(-2, 1),
            new Vector2Int(-1, 2)
        };

        for (int i = 0; i < offsets.Length; i++)
        {
            Vector2Int target = piece.boardPosition + offsets[i];
            if (!IsInsideBoard(target))
                continue;

            ChessPiece targetPiece = board[target.x, target.y];
            if (targetPiece == null)
            {
                moves.Add(CreateBasicMove(piece, target));
            }
            else if (targetPiece.pieceColor != piece.pieceColor)
            {
                ChessMove move = CreateBasicMove(piece, target);
                move.isCapture = true;
                move.capturedPiece = targetPiece;
                moves.Add(move);
            }
        }
    }

    void AddKingMoves(ChessPiece piece, List<ChessMove> moves)
    {
        for (int x = -1; x <= 1; x++)
        {
            for (int y = -1; y <= 1; y++)
            {
                if (x == 0 && y == 0)
                    continue;

                Vector2Int target = piece.boardPosition + new Vector2Int(x, y);
                if (!IsInsideBoard(target))
                    continue;

                ChessPiece targetPiece = board[target.x, target.y];
                if (targetPiece == null)
                {
                    moves.Add(CreateBasicMove(piece, target));
                }
                else if (targetPiece.pieceColor != piece.pieceColor)
                {
                    ChessMove move = CreateBasicMove(piece, target);
                    move.isCapture = true;
                    move.capturedPiece = targetPiece;
                    moves.Add(move);
                }
            }
        }

        AddCastleMoves(piece, moves);
    }

    void AddCastleMoves(ChessPiece king, List<ChessMove> moves)
    {
        if (king.pieceType != PieceType.King)
            return;

        if (king.pieceColor == PieceColor.White)
        {
            if (whiteKingMoved)
                return;

            if (!IsSquareAttacked(king.boardPosition, PieceColor.Black))
            {
                if (!whiteRightRookMoved && CanCastlePathBeUsed(new Vector2Int(5, 0), new Vector2Int(6, 0)) &&
                    !IsSquareAttacked(new Vector2Int(5, 0), PieceColor.Black) &&
                    !IsSquareAttacked(new Vector2Int(6, 0), PieceColor.Black))
                {
                    ChessPiece rook = board[7, 0];
                    if (rook != null && rook.pieceType == PieceType.Rook && rook.pieceColor == PieceColor.White)
                    {
                        ChessMove move = CreateBasicMove(king, new Vector2Int(6, 0));
                        move.isCastle = true;
                        move.castleRook = rook;
                        move.rookFrom = new Vector2Int(7, 0);
                        move.rookTo = new Vector2Int(5, 0);
                        moves.Add(move);
                    }
                }

                if (!whiteLeftRookMoved && CanCastlePathBeUsed(new Vector2Int(1, 0), new Vector2Int(2, 0), new Vector2Int(3, 0)) &&
                    !IsSquareAttacked(new Vector2Int(3, 0), PieceColor.Black) &&
                    !IsSquareAttacked(new Vector2Int(2, 0), PieceColor.Black))
                {
                    ChessPiece rook = board[0, 0];
                    if (rook != null && rook.pieceType == PieceType.Rook && rook.pieceColor == PieceColor.White)
                    {
                        ChessMove move = CreateBasicMove(king, new Vector2Int(2, 0));
                        move.isCastle = true;
                        move.castleRook = rook;
                        move.rookFrom = new Vector2Int(0, 0);
                        move.rookTo = new Vector2Int(3, 0);
                        moves.Add(move);
                    }
                }
            }
        }
        else
        {
            if (blackKingMoved)
                return;

            if (!IsSquareAttacked(king.boardPosition, PieceColor.White))
            {
                if (!blackRightRookMoved && CanCastlePathBeUsed(new Vector2Int(5, 7), new Vector2Int(6, 7)) &&
                    !IsSquareAttacked(new Vector2Int(5, 7), PieceColor.White) &&
                    !IsSquareAttacked(new Vector2Int(6, 7), PieceColor.White))
                {
                    ChessPiece rook = board[7, 7];
                    if (rook != null && rook.pieceType == PieceType.Rook && rook.pieceColor == PieceColor.Black)
                    {
                        ChessMove move = CreateBasicMove(king, new Vector2Int(6, 7));
                        move.isCastle = true;
                        move.castleRook = rook;
                        move.rookFrom = new Vector2Int(7, 7);
                        move.rookTo = new Vector2Int(5, 7);
                        moves.Add(move);
                    }
                }

                if (!blackLeftRookMoved && CanCastlePathBeUsed(new Vector2Int(1, 7), new Vector2Int(2, 7), new Vector2Int(3, 7)) &&
                    !IsSquareAttacked(new Vector2Int(3, 7), PieceColor.White) &&
                    !IsSquareAttacked(new Vector2Int(2, 7), PieceColor.White))
                {
                    ChessPiece rook = board[0, 7];
                    if (rook != null && rook.pieceType == PieceType.Rook && rook.pieceColor == PieceColor.Black)
                    {
                        ChessMove move = CreateBasicMove(king, new Vector2Int(2, 7));
                        move.isCastle = true;
                        move.castleRook = rook;
                        move.rookFrom = new Vector2Int(0, 7);
                        move.rookTo = new Vector2Int(3, 7);
                        moves.Add(move);
                    }
                }
            }
        }
    }

    bool CanCastlePathBeUsed(params Vector2Int[] squares)
    {
        for (int i = 0; i < squares.Length; i++)
        {
            if (!IsInsideBoard(squares[i]) || board[squares[i].x, squares[i].y] != null)
                return false;
        }

        return true;
    }

    ChessMove CreateBasicMove(ChessPiece piece, Vector2Int to)
    {
        return new ChessMove
        {
            piece = piece,
            from = piece.boardPosition,
            to = to
        };
    }

    bool IsMoveLegal(ChessPiece piece, ChessMove move)
    {
        SimState state = ApplySimulatedMove(move);
        bool kingSafe = !IsKingInCheck(piece.pieceColor);
        UndoSimulatedMove(state);
        return kingSafe;
    }

    struct SimState
    {
        public ChessMove move;
        public PieceType originalPieceType;
        public Vector2Int originalPiecePosition;
        public Vector2Int? previousEnPassantTarget;
        public ChessPiece previousEnPassantPawn;
    }

    SimState ApplySimulatedMove(ChessMove move)
    {
        SimState state = new SimState
        {
            move = move,
            originalPieceType = move.piece.pieceType,
            originalPiecePosition = move.piece.boardPosition,
            previousEnPassantTarget = enPassantTargetSquare,
            previousEnPassantPawn = enPassantPawn
        };

        board[move.from.x, move.from.y] = null;

        if (move.isEnPassant && move.enPassantCapturedPiece != null)
        {
            board[move.enPassantCapturedPos.x, move.enPassantCapturedPos.y] = null;
        }
        else if (move.isCapture && move.capturedPiece != null)
        {
            board[move.to.x, move.to.y] = null;
        }

        move.piece.boardPosition = move.to;
        board[move.to.x, move.to.y] = move.piece;

        if (move.isCastle && move.castleRook != null)
        {
            board[move.rookFrom.x, move.rookFrom.y] = null;
            move.castleRook.boardPosition = move.rookTo;
            board[move.rookTo.x, move.rookTo.y] = move.castleRook;
        }

        if (move.isPromotion)
        {
            move.piece.pieceType = move.promotionType;
        }

        enPassantTargetSquare = null;
        enPassantPawn = null;

        return state;
    }

    void UndoSimulatedMove(SimState state)
    {
        ChessMove move = state.move;

        if (move.isCastle && move.castleRook != null)
        {
            board[move.rookTo.x, move.rookTo.y] = null;
            move.castleRook.boardPosition = move.rookFrom;
            board[move.rookFrom.x, move.rookFrom.y] = move.castleRook;
        }

        board[move.to.x, move.to.y] = null;

        move.piece.boardPosition = state.originalPiecePosition;
        move.piece.pieceType = state.originalPieceType;
        board[move.from.x, move.from.y] = move.piece;

        if (move.isEnPassant && move.enPassantCapturedPiece != null)
        {
            board[move.enPassantCapturedPos.x, move.enPassantCapturedPos.y] = move.enPassantCapturedPiece;
        }
        else if (move.isCapture && move.capturedPiece != null)
        {
            board[move.to.x, move.to.y] = move.capturedPiece;
        }

        enPassantTargetSquare = state.previousEnPassantTarget;
        enPassantPawn = state.previousEnPassantPawn;
    }

    bool IsKingInCheck(PieceColor color)
    {
        ChessPiece king = FindKing(color);
        if (king == null)
            return false;

        return IsSquareAttacked(king.boardPosition, OpponentOf(color));
    }

    ChessPiece FindKing(PieceColor color)
    {
        for (int x = 0; x < 8; x++)
        {
            for (int y = 0; y < 8; y++)
            {
                ChessPiece piece = board[x, y];
                if (piece != null && piece.pieceColor == color && piece.pieceType == PieceType.King)
                    return piece;
            }
        }

        return null;
    }

    bool IsSquareAttacked(Vector2Int square, PieceColor byColor)
    {
        int pawnDir = byColor == PieceColor.White ? 1 : -1;
        Vector2Int pawnLeft = new Vector2Int(square.x - 1, square.y - pawnDir);
        Vector2Int pawnRight = new Vector2Int(square.x + 1, square.y - pawnDir);

        if (IsPawnAttacker(pawnLeft, byColor) || IsPawnAttacker(pawnRight, byColor))
            return true;

        Vector2Int[] knightOffsets = new Vector2Int[]
        {
            new Vector2Int(1, 2),
            new Vector2Int(2, 1),
            new Vector2Int(2, -1),
            new Vector2Int(1, -2),
            new Vector2Int(-1, -2),
            new Vector2Int(-2, -1),
            new Vector2Int(-2, 1),
            new Vector2Int(-1, 2)
        };

        for (int i = 0; i < knightOffsets.Length; i++)
        {
            Vector2Int pos = square + knightOffsets[i];
            if (IsInsideBoard(pos))
            {
                ChessPiece piece = board[pos.x, pos.y];
                if (piece != null && piece.pieceColor == byColor && piece.pieceType == PieceType.Knight)
                    return true;
            }
        }

        Vector2Int[] rookDirs = new Vector2Int[]
        {
            Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right
        };

        for (int i = 0; i < rookDirs.Length; i++)
        {
            if (RayHasAttacker(square, rookDirs[i], byColor, PieceType.Rook, PieceType.Queen))
                return true;
        }

        Vector2Int[] bishopDirs = new Vector2Int[]
        {
            new Vector2Int(1, 1),
            new Vector2Int(1, -1),
            new Vector2Int(-1, 1),
            new Vector2Int(-1, -1)
        };

        for (int i = 0; i < bishopDirs.Length; i++)
        {
            if (RayHasAttacker(square, bishopDirs[i], byColor, PieceType.Bishop, PieceType.Queen))
                return true;
        }

        for (int x = -1; x <= 1; x++)
        {
            for (int y = -1; y <= 1; y++)
            {
                if (x == 0 && y == 0)
                    continue;

                Vector2Int pos = square + new Vector2Int(x, y);
                if (IsInsideBoard(pos))
                {
                    ChessPiece piece = board[pos.x, pos.y];
                    if (piece != null && piece.pieceColor == byColor && piece.pieceType == PieceType.King)
                        return true;
                }
            }
        }

        return false;
    }

    bool IsPawnAttacker(Vector2Int pos, PieceColor color)
    {
        if (!IsInsideBoard(pos))
            return false;

        ChessPiece piece = board[pos.x, pos.y];
        return piece != null && piece.pieceColor == color && piece.pieceType == PieceType.Pawn;
    }

    bool RayHasAttacker(Vector2Int start, Vector2Int dir, PieceColor color, PieceType t1, PieceType t2)
    {
        Vector2Int current = start + dir;

        while (IsInsideBoard(current))
        {
            ChessPiece piece = board[current.x, current.y];
            if (piece != null)
            {
                if (piece.pieceColor == color && (piece.pieceType == t1 || piece.pieceType == t2))
                    return true;

                return false;
            }

            current += dir;
        }

        return false;
    }

    void MakeRealMove(ChessMove move)
    {
        bool pawnMove = move.piece.pieceType == PieceType.Pawn;
        bool capture = move.isCapture;

        UpdateCastlingRightsBeforeMove(move);
        ClearCapturedRookRights(move);

        board[move.from.x, move.from.y] = null;

        bool defectionHappened = false;

        if (move.isCapture)
        {
            ChessPiece targetPiece = move.isEnPassant ? move.enPassantCapturedPiece : move.capturedPiece;

            if (targetPiece != null && ShouldDefect(move))
            {
                defectionHappened = true;

                Vector2Int targetOriginalPos = move.isEnPassant ? move.enPassantCapturedPos : move.to;

                board[targetOriginalPos.x, targetOriginalPos.y] = null;

                ConvertPieceToColor(targetPiece, move.piece.pieceColor);

                targetPiece.boardPosition = move.from;
                board[move.from.x, move.from.y] = targetPiece;
                targetPiece.transform.position = BoardToWorld(move.from);
            }
            else
            {
                if (move.isEnPassant && move.enPassantCapturedPiece != null)
                {
                    RemoveFrozenStateFromPiece(move.enPassantCapturedPiece);
                    board[move.enPassantCapturedPos.x, move.enPassantCapturedPos.y] = null;

                    if (move.enPassantCapturedPiece.gameObject != null)
                        Destroy(move.enPassantCapturedPiece.gameObject);
                }
                else if (move.capturedPiece != null)
                {
                    RemoveFrozenStateFromPiece(move.capturedPiece);
                    board[move.to.x, move.to.y] = null;

                    if (move.capturedPiece.gameObject != null)
                        Destroy(move.capturedPiece.gameObject);
                }
            }
        }

        move.piece.boardPosition = move.to;
        board[move.to.x, move.to.y] = move.piece;
        move.piece.transform.position = BoardToWorld(move.to);

        if (move.isCastle && move.castleRook != null)
        {
            board[move.rookFrom.x, move.rookFrom.y] = null;
            move.castleRook.boardPosition = move.rookTo;
            board[move.rookTo.x, move.rookTo.y] = move.castleRook;
            move.castleRook.transform.position = BoardToWorld(move.rookTo);
        }

        if (move.isPromotion)
        {
            move.piece.pieceType = move.promotionType;
            SpriteRenderer sr = move.piece.GetComponent<SpriteRenderer>();
            if (sr != null)
                sr.sprite = GetSprite(move.piece.pieceType, move.piece.pieceColor);
        }

        if (move.isDoublePawnPush)
        {
            int direction = move.piece.pieceColor == PieceColor.White ? 1 : -1;
            enPassantTargetSquare = new Vector2Int(move.from.x, move.from.y + direction);
            enPassantPawn = move.piece;
        }
        else
        {
            enPassantTargetSquare = null;
            enPassantPawn = null;
        }

        if (pawnMove || capture)
            halfmoveClock = 0;
        else
            halfmoveClock++;

        if (currentTurn == PieceColor.Black)
            fullmoveNumber++;

        currentTurn = OpponentOf(currentTurn);
        RecordCurrentPosition();

        AdvanceFrozenPieceTimers(currentTurn);

        if (!IsAnyWeatherActive())
        {
            TryTriggerSnowstorm();

            if (!IsAnyWeatherActive())
            {
                TryTriggerThunderstorm();
            }

            if (!IsAnyWeatherActive())
            {
                TryTriggerTornado();
            }
        }

        if (defectionHappened)
        {
            Debug.Log("Defection! The attacked piece switched sides instead of being captured.");
        }
        TryTriggerThunderstorm();
    }

    void UpdateCastlingRightsBeforeMove(ChessMove move)
    {
        if (move.piece.pieceType == PieceType.King)
        {
            if (move.piece.pieceColor == PieceColor.White)
                whiteKingMoved = true;
            else
                blackKingMoved = true;
        }

        if (move.piece.pieceType == PieceType.Rook)
        {
            if (move.piece.pieceColor == PieceColor.White)
            {
                if (move.from == new Vector2Int(0, 0))
                    whiteLeftRookMoved = true;
                else if (move.from == new Vector2Int(7, 0))
                    whiteRightRookMoved = true;
            }
            else
            {
                if (move.from == new Vector2Int(0, 7))
                    blackLeftRookMoved = true;
                else if (move.from == new Vector2Int(7, 7))
                    blackRightRookMoved = true;
            }
        }
    }

    void ClearCapturedRookRights(ChessMove move)
    {
        ChessPiece captured = move.isEnPassant ? null : move.capturedPiece;
        if (captured == null || captured.pieceType != PieceType.Rook)
            return;

        if (captured.pieceColor == PieceColor.White)
        {
            if (move.to == new Vector2Int(0, 0))
                whiteLeftRookMoved = true;
            else if (move.to == new Vector2Int(7, 0))
                whiteRightRookMoved = true;
        }
        else
        {
            if (move.to == new Vector2Int(0, 7))
                blackLeftRookMoved = true;
            else if (move.to == new Vector2Int(7, 7))
                blackRightRookMoved = true;
        }
    }

    void EvaluateGameStateAfterMove()
    {
        PieceColor sideToMove = currentTurn;
        bool inCheck = IsKingInCheck(sideToMove);
        List<ChessMove> moves = GenerateAllLegalMovesForColor(sideToMove);

        if (moves.Count == 0)
        {
            gameOver = true;

            if (inCheck)
            {
                gameOverMessage = OpponentOf(sideToMove) + " wins by checkmate.";
            }
            else
            {
                gameOverMessage = "Draw by stalemate.";
            }

            Debug.Log(gameOverMessage);
            return;
        }

        if (IsInsufficientMaterial())
        {
            gameOver = true;
            gameOverMessage = "Draw by insufficient material.";
            Debug.Log(gameOverMessage);
            return;
        }

        string currentKey = BuildPositionKey();
        if (repetitionTable.ContainsKey(currentKey) && repetitionTable[currentKey] >= 3)
        {
            gameOver = true;
            gameOverMessage = "Draw by threefold repetition.";
            Debug.Log(gameOverMessage);
            return;
        }

        if (halfmoveClock >= 100)
        {
            gameOver = true;
            gameOverMessage = "Draw by 50-move rule.";
            Debug.Log(gameOverMessage);
            return;
        }

        if (inCheck)
        {
            Debug.Log(sideToMove + " is in check.");
        }
    }

    bool IsInsufficientMaterial()
    {
        List<ChessPiece> whitePieces = new List<ChessPiece>();
        List<ChessPiece> blackPieces = new List<ChessPiece>();

        for (int x = 0; x < 8; x++)
        {
            for (int y = 0; y < 8; y++)
            {
                ChessPiece piece = board[x, y];
                if (piece == null)
                    continue;

                if (piece.pieceColor == PieceColor.White)
                    whitePieces.Add(piece);
                else
                    blackPieces.Add(piece);
            }
        }

        if (HasMaterialForMate(whitePieces) || HasMaterialForMate(blackPieces))
            return false;

        if (whitePieces.Count == 1 && blackPieces.Count == 1)
            return true;

        if (whitePieces.Count == 2 && blackPieces.Count == 1 && HasOnlyMinorPiece(whitePieces))
            return true;

        if (blackPieces.Count == 2 && whitePieces.Count == 1 && HasOnlyMinorPiece(blackPieces))
            return true;

        if (whitePieces.Count == 2 && blackPieces.Count == 2 &&
            HasOnlyBishop(whitePieces) && HasOnlyBishop(blackPieces))
        {
            Vector2Int whiteBishopPos = GetOnlyNonKingPiece(whitePieces).boardPosition;
            Vector2Int blackBishopPos = GetOnlyNonKingPiece(blackPieces).boardPosition;

            bool whiteDark = IsDarkSquare(whiteBishopPos);
            bool blackDark = IsDarkSquare(blackBishopPos);

            if (whiteDark == blackDark)
                return true;
        }

        return false;
    }

    bool HasMaterialForMate(List<ChessPiece> pieces)
    {
        for (int i = 0; i < pieces.Count; i++)
        {
            PieceType type = pieces[i].pieceType;

            if (type == PieceType.Pawn || type == PieceType.Rook || type == PieceType.Queen)
                return true;
        }

        int bishops = 0;
        int knights = 0;

        for (int i = 0; i < pieces.Count; i++)
        {
            if (pieces[i].pieceType == PieceType.Bishop) bishops++;
            if (pieces[i].pieceType == PieceType.Knight) knights++;
        }

        if (bishops >= 2)
            return true;

        if (knights >= 2)
            return true;

        if (bishops >= 1 && knights >= 1)
            return true;

        return false;
    }

    bool HasOnlyMinorPiece(List<ChessPiece> pieces)
    {
        ChessPiece extra = GetOnlyNonKingPiece(pieces);
        return extra != null && (extra.pieceType == PieceType.Bishop || extra.pieceType == PieceType.Knight);
    }

    bool HasOnlyBishop(List<ChessPiece> pieces)
    {
        ChessPiece extra = GetOnlyNonKingPiece(pieces);
        return extra != null && extra.pieceType == PieceType.Bishop;
    }

    ChessPiece GetOnlyNonKingPiece(List<ChessPiece> pieces)
    {
        for (int i = 0; i < pieces.Count; i++)
        {
            if (pieces[i].pieceType != PieceType.King)
                return pieces[i];
        }

        return null;
    }

    bool IsDarkSquare(Vector2Int pos)
    {
        return (pos.x + pos.y) % 2 == 1;
    }

    void RecordCurrentPosition()
    {
        string key = BuildPositionKey();
        if (repetitionTable.ContainsKey(key))
            repetitionTable[key]++;
        else
            repetitionTable[key] = 1;
    }

    string BuildPositionKey()
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();

        for (int y = 0; y < 8; y++)
        {
            for (int x = 0; x < 8; x++)
            {
                ChessPiece piece = board[x, y];
                if (piece == null)
                {
                    sb.Append('.');
                }
                else
                {
                    sb.Append(piece.pieceColor == PieceColor.White ? 'W' : 'B');
                    sb.Append(PieceTypeToChar(piece.pieceType));
                }
            }
        }

        sb.Append('|');
        sb.Append(currentTurn == PieceColor.White ? 'W' : 'B');
        sb.Append('|');
        sb.Append(whiteKingMoved ? '1' : '0');
        sb.Append(blackKingMoved ? '1' : '0');
        sb.Append(whiteLeftRookMoved ? '1' : '0');
        sb.Append(whiteRightRookMoved ? '1' : '0');
        sb.Append(blackLeftRookMoved ? '1' : '0');
        sb.Append(blackRightRookMoved ? '1' : '0');
        sb.Append('|');

        if (enPassantTargetSquare.HasValue)
        {
            sb.Append(enPassantTargetSquare.Value.x);
            sb.Append(',');
            sb.Append(enPassantTargetSquare.Value.y);
        }
        else
        {
            sb.Append('-');
        }

        return sb.ToString();
    }

    char PieceTypeToChar(PieceType type)
    {
        switch (type)
        {
            case PieceType.Pawn: return 'P';
            case PieceType.Rook: return 'R';
            case PieceType.Knight: return 'N';
            case PieceType.Bishop: return 'B';
            case PieceType.Queen: return 'Q';
            case PieceType.King: return 'K';
        }

        return '?';
    }

    PieceColor OpponentOf(PieceColor color)
    {
        return color == PieceColor.White ? PieceColor.Black : PieceColor.White;
    }

    void ShowHighlights(List<ChessMove> moves)
    {
        ClearHighlights();

        for (int i = 0; i < moves.Count; i++)
        {
            GameObject highlightObj = new GameObject("Highlight");
            highlightObj.transform.SetParent(highlightParent);
            highlightObj.transform.position = BoardToWorld(moves[i].to);
            highlightObj.transform.localScale = Vector3.one * tileSize * 0.95f;

            SpriteRenderer sr = highlightObj.AddComponent<SpriteRenderer>();
            sr.sprite = GetHighlightSprite();
            sr.color = highlightColor;
            sr.sortingOrder = 5;

            highlightObj.AddComponent<MoveHighlight>();
            highlights.Add(highlightObj);
        }
    }

    void ClearHighlights()
    {
        for (int i = 0; i < highlights.Count; i++)
        {
            if (highlights[i] != null)
                Destroy(highlights[i]);
        }

        highlights.Clear();
    }

    Sprite GetHighlightSprite()
    {
        if (cachedHighlightSprite == null)
        {
            Texture2D tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();

            cachedHighlightSprite = Sprite.Create(
                tex,
                new Rect(0, 0, 1, 1),
                new Vector2(0.5f, 0.5f),
                1f
            );
        }

        return cachedHighlightSprite;
    }

    Vector3 BoardToWorld(Vector2Int boardPos)
    {
        return new Vector3(
            boardOrigin.x + boardPos.x * tileSize,
            boardOrigin.y + boardPos.y * tileSize,
            0f
        );
    }

    Vector2Int WorldToBoard(Vector3 worldPos)
    {
        int x = Mathf.RoundToInt((worldPos.x - boardOrigin.x) / tileSize);
        int y = Mathf.RoundToInt((worldPos.y - boardOrigin.y) / tileSize);
        return new Vector2Int(x, y);
    }

    bool IsInsideBoard(Vector2Int pos)
    {
        return pos.x >= 0 && pos.x < 8 && pos.y >= 0 && pos.y < 8;
    }
}