using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "GenreDatabase", menuName = "Scriptable Objects/GenreDatabase")]
public class GenreDatabase : ScriptableObject
{
    public List<GenreSO> genres;
}
