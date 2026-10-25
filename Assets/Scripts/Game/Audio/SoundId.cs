namespace KingsRescue.Audio
{
    /// <summary>
    /// One slot in <see cref="SoundBank"/>. Clips stay empty until they are assigned.
    /// </summary>
    public enum SoundId
    {
        None = 0,
        UiClick = 1,
        UiConfirm = 2,
        PlayerFootstep = 3,
        PlayerAttack = 4,
        PlayerShoot = 5,
        PlayerHurt = 6,
        PlayerDeath = 7,
        PlayerHeal = 8,
        PlayerLevelUp = 9,
        PowerUp = 10,
        PowerUpMax = 11,
        PickupCoin = 12,
        PickupGem = 13,
        PickupPurse = 14,
        ChestOpen = 15,
        PropBreak = 16,
        EnemyHit = 17,
        EnemyDeath = 18,
        BossDeath = 19,
        EnemyAttack = 20,
        EnemySpell = 21,
        SpellCast = 22,
        SpellStrike = 23
    }
}
