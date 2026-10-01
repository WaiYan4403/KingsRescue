/// <summary>
/// All possible ingredients that can be added to a bowl.
/// </summary>
public enum IngredientType
{
    None,
    SoftNoodle,         // Produced by the Boiler after processing raw noodle
    CookedOil,          // ကြက်သွန်ဆီချက်
    PalNgaPi,           // ပဲငပိ
    KyarNyo,            // ကြာညို့
    PalNgyanPyarYay,    // ပဲငံပြာရည်
    MiniLeafs,          // ကြက်သွန်မြိတ်
    Chicken,
    Pork,
    Soup
}

/// <summary>
/// Mutually-exclusive ingredient groups — only one ingredient per group may be added to a bowl.
/// e.g. Chicken and Pork both belong to the Meat group, so only one can be chosen.
/// </summary>
public enum ExclusiveGroup
{
    None,
    Meat
}
