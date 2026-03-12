using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;

namespace BlazorClaw.Core.Utils
{
    public class SaveableJsonConfigurationSource : JsonConfigurationSource
    {
        /// <summary>
        /// Builds the <see cref="JsonConfigurationProvider"/> for this source.
        /// </summary>
        /// <param name="builder">The <see cref="IConfigurationBuilder"/>.</param>
        /// <returns>A <see cref="JsonConfigurationProvider"/></returns>
        public override IConfigurationProvider Build(IConfigurationBuilder builder)
        {
            EnsureDefaults(builder);
            return new SaveableJsonConfigurationProvider(this);
        }
    }
}
