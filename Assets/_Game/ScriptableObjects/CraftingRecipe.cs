using UnityEngine;

/// <summary>
/// A crafting recipe: a set of ingredient items (with quantities) that combine
/// into a result item. Instances live in ScriptableObjects/Recipes/ and are
/// matched against the player's inventory by the CraftingSystem.
/// </summary>
[CreateAssetMenu(fileName = "SO_Recipe_New", menuName = "Game/Items/Crafting Recipe")]
public class CraftingRecipe : ScriptableObject
{
    [Header("Identity")]
    public string recipeId;
    public string displayName;

    [Header("Ingredients")]
    public RecipeIngredient[] ingredients;

    [Header("Output")]
    public ItemData resultItem;
    public int resultQuantity = 1;
}

/// <summary>A single required ingredient in a <see cref="CraftingRecipe"/>.</summary>
[System.Serializable]
public struct RecipeIngredient
{
    public ItemData item;
    public int quantity;
}
