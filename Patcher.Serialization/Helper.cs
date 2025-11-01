using Mutagen.Bethesda.Serialization.Newtonsoft;
using Mutagen.Bethesda.Skyrim;
using Noggog;

namespace Patcher.Serialization
{
    public static class Helper
    {
        private static Task Hint(ISkyrimModGetter mod) => MutagenJsonConverter.Instance.Serialize(mod, "");

        public static async Task<ISkyrimMod> DeserializeFromPath(DirectoryPath path)
        {
            return await MutagenJsonConverter.Instance.Deserialize(path);
        }
    }
}
