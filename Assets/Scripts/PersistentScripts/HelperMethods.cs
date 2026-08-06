using System.Collections.Generic;
using UnityEngine;

public static class HelperMethods
{
    // Uses System.Random so it's 100% safe to use in constructors
    public static void ShuffleList<T>(this List<T> list)
    {
        // Create a standard C# random instance
        System.Random rng = new System.Random();

        for (int i = list.Count - 1; i > 0; i--)
        {
            // rng.Next(min, maxExclusive) behaves exactly like Random.Range
            int randomIndex = rng.Next(0, i + 1);

            // Swap elements
            T temp = list[i];
            list[i] = list[randomIndex];
            list[randomIndex] = temp;
        }
    }
}
