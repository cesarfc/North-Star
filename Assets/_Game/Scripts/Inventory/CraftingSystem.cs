using System;
using System.Collections.Generic;
using UnityEngine;

namespace NorthStar.Inventory
{
    /// <summary>
    /// Matches <see cref="CraftingRecipe"/> definitions against the player's
    /// <see cref="Inventory"/> and combines ingredients into results. Recipe
    /// matching is pure (see <see cref="CanCraft"/>); <see cref="Craft"/> mutates
    /// the inventory by consuming ingredients and adding the output. Lives in the
    /// Inventory module and talks only to its own Inventory — no cross-module refs.
    /// </summary>
    public class CraftingSystem : MonoBehaviour
    {
        [Tooltip("All recipes this station/system can craft.")]
        [SerializeField] private List<CraftingRecipe> _recipes = new List<CraftingRecipe>();

        [Tooltip("The inventory ingredients are drawn from and results added to.")]
        [SerializeField] private Inventory _inventory;

        /// <summary>Raised after a successful craft. Args: (recipe, resultQuantity).</summary>
        public event Action<CraftingRecipe, int> OnCrafted;

        /// <summary>
        /// True if every ingredient of <paramref name="recipe"/> is present in
        /// <paramref name="inventory"/> in at least the required quantity. Returns
        /// false for a null recipe/inventory or a recipe with no ingredients.
        /// </summary>
        public static bool CanCraft(CraftingRecipe recipe, Inventory inventory)
        {
            if (recipe == null || inventory == null) return false;
            if (recipe.ingredients == null || recipe.ingredients.Length == 0) return false;

            foreach (var ing in recipe.ingredients)
            {
                if (ing.item == null || ing.quantity <= 0) return false;
                if (inventory.GetItemCount(ing.item.itemId) < ing.quantity) return false;
            }
            return true;
        }

        /// <summary>
        /// True if the configured inventory currently has the ingredients for
        /// <paramref name="recipe"/>.
        /// </summary>
        public bool CanCraft(CraftingRecipe recipe) => CanCraft(recipe, _inventory);

        /// <summary>
        /// Find the first known recipe whose ingredients the configured inventory
        /// can currently satisfy, or null if none match.
        /// </summary>
        public CraftingRecipe FindCraftable()
        {
            foreach (var recipe in _recipes)
                if (CanCraft(recipe, _inventory))
                    return recipe;
            return null;
        }

        /// <summary>
        /// Consume the recipe's ingredients and add its result to the configured
        /// inventory. No-op returning false if the ingredients are not all present
        /// or the result item is missing. The ingredient check happens before any
        /// removal, so a failed craft never partially consumes items.
        /// </summary>
        public bool Craft(CraftingRecipe recipe)
        {
            if (!CanCraft(recipe, _inventory)) return false;
            if (recipe.resultItem == null) return false;

            foreach (var ing in recipe.ingredients)
                _inventory.RemoveItem(ing.item.itemId, ing.quantity);

            int qty = Mathf.Max(1, recipe.resultQuantity);
            _inventory.AddItem(recipe.resultItem, qty);

            OnCrafted?.Invoke(recipe, qty);
            return true;
        }
    }
}
