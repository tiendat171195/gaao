using UnityEngine;

namespace PawFit.Pet.View
{
    /// <summary>Dựng hình thú cho 1 GameObject: ưu tiên model Gen 1, không nạp được thì dùng thú tạm bằng khối.</summary>
    public static class PetAvatarFactory
    {
        public static IPetAvatar Create(GameObject host, PetDna dna, bool forcePlaceholder = false)
        {
            if (!forcePlaceholder && Gen1PetAvatar.KitAvailable)
            {
                var gen1 = host.AddComponent<Gen1PetAvatar>();
                gen1.Build(dna);
                if (gen1.IsBuilt) return gen1;
                Object.Destroy(gen1);
            }
            var placeholder = host.AddComponent<PlaceholderPetAvatar>();
            placeholder.Build(dna);
            return placeholder;
        }
    }
}
