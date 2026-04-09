
public interface IDamageable
{
    public int CurrentHealth {set; get; }
    public int MaxHealth { set; get; }
    public void TakeDamage(int damage,AttackType attackType )
    {
        CurrentHealth -= damage;
        if (CurrentHealth <= 0)
        {
            Die();
        }
    }
    public void RaiseHealthSnapshot()
    {
        
    }
    public void Die()
    {
    }
}