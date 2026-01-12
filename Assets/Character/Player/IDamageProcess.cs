    using System.Collections;
    using Cysharp.Threading.Tasks;

    namespace Character.Player
    {
        public interface IDamageProcess
        {  
            public void OnTakeDamage(float damage);
        }
    }

