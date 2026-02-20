using Microsoft.Agents.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GenesysHandoff.Services
{
    /// <summary>
    /// Processes and fixes citation entities for proper rendering in Teams and other clients.
    /// </summary>
    public static class CitationEntityProcessor
    {
        /// <summary>
        /// Filters entities to exclude streaminfo types and fixes invalid citation appearances.
        /// Teams requires proper citation structure for interactive citation rendering.
        /// </summary>
        /// <param name="entities">The original entities from Copilot Studio.</param>
        /// <returns>A filtered list of entities with valid citation formatting.</returns>
        public static IList<Entity> FixCitationEntities(IList<Entity> entities)
        {
            var filteredEntities = new List<Entity>();
            foreach (var entity in entities)
            {
                // Exclude streaminfo entities
                if (entity.Type != null && entity.Type.Equals("streaminfo", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // Process AIEntity citations to ensure proper Teams rendering
                if (entity is AIEntity aiEntity)
                {
                    if (aiEntity.Citation != null && aiEntity.Citation.Count > 0)
                    {
                        var annotation = new AIEntity();
                        foreach (var clientCitation in aiEntity.Citation)
                        {
                            if (clientCitation.Appearance == null)
                            {
                                continue;
                            }

                            var clientCitationIconName = GetIconNameOrDefault(clientCitation.Appearance.Image?.Name);
                            
                            annotation.Citation.Add(new ClientCitation(
                                clientCitation.Position,
                                clientCitation.Appearance.Name,
                                clientCitation.Appearance.Abstract,
                                clientCitation.Appearance.Text ?? string.Empty,
                                null,
                                clientCitation.Appearance.Url,
                                clientCitationIconName
                            ));
                        }
                        filteredEntities.Add(annotation);
                    }
                    else
                    {
                        filteredEntities.Add(entity);
                    }
                }
            }
            return filteredEntities;
        }

        /// <summary>
        /// Gets the icon name from the appearance image, defaulting to Image if the value is unknown or invalid.
        /// </summary>
        private static ClientCitationsIconNameEnum GetIconNameOrDefault(ClientCitationsIconNameEnum? iconName)
        {
            if (iconName == null)
            {
                return ClientCitationsIconNameEnum.Image;
            }

            // Check if the enum value is defined, otherwise use default
            if (!Enum.IsDefined(typeof(ClientCitationsIconNameEnum), iconName.Value))
            {
                return ClientCitationsIconNameEnum.Image;
            }

            return iconName.Value;
        }
    }
}
