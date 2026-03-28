using UnityEngine;

public enum PieceType
{
    Pawn,
    Rook,
    Knight,
    Bishop,
    Queen,
    King
}

public enum PieceColor
{
    White,
    Black
}

public class ChessPiece : MonoBehaviour
{
    public PieceType pieceType;
    public PieceColor pieceColor;
    public Vector2Int boardPosition;
}
