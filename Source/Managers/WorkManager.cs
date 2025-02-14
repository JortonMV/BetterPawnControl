﻿using System.Collections.Generic;
using Verse;
using RimWorld;
using System.Linq;
using System;

namespace BetterPawnControl
{
    [StaticConstructorOnStartup]
    class WorkManager : Manager<WorkLink>
    {
        internal static List<WorkLink> clipboard = new List<WorkLink>();

        internal static void DeletePolicy(Policy policy)
        {
            //delete if not default AssignPolicy
            if (policy != null && policy.id > 0)
            {
                links.RemoveAll(x => x.zone == policy.id);
                policies.Remove(policy);
                int mapId = Find.CurrentMap.uniqueID;
                foreach (MapActivePolicy m in activePolicies)
                {
                    if (m.activePolicy.id == policy.id)
                    {
                        m.activePolicy = policies[0];
                        DirtyPolicy = true;
                    }
                }
            }
        }

        internal static void DeleteLinksInMap(int mapId)
        {
            links.RemoveAll(x => x.mapId == mapId);
        }

        internal static void DeleteMap(MapActivePolicy map)
        {
            activePolicies.Remove(map);
        }

        internal static void SaveCurrentState(List<Pawn> pawns)
        {
			int currentMap = Find.CurrentMap.uniqueID;
            //Save current state
            foreach (Pawn p in pawns)
            {	
                //find colonist in the current zone in the current map
                WorkLink link = WorkManager.links.Find(
					x => x.colonist.Equals(p) &&
					x.zone == WorkManager.GetActivePolicy().id &&
					x.mapId == currentMap);

				if (link != null)
                {
					//colonist found! save  
					WorkManager.SavePawnPriorities(p, link);
				}
                else
                {
                    //colonist not found. So add it to the WorkLink list
                    link = new WorkLink(
                        WorkManager.GetActivePolicy().id,
                        p,
                        new Dictionary<WorkTypeDef, int>(),
                        currentMap);
                    WorkManager.links.Add(link);
                    WorkManager.SavePawnPriorities(p, link);
                }
			}
        }

        internal static void CleanDeadColonists(List<Pawn> pawns)
        {
            for (int i = 0; i < WorkManager.links.Count; i++)
            {
                WorkLink pawn = WorkManager.links[i];
                if (!pawns.Contains(pawn.colonist))
                {
                    if (pawn.colonist == null || pawn.colonist.Dead)
                    {
                        WorkManager.links.Remove(pawn);
                    }
                }
            }
        }

        internal static bool ActivePoliciesContainsValidMap()
        {
            bool containsValidMap = false;
            foreach (Map map in Find.Maps)
            {
                if (WorkManager.activePolicies.Any(x => x.mapId == map.uniqueID))
                {
                    containsValidMap = true;
                    break;
                }
            }
            return containsValidMap;
        }

        internal static void CleanDeadMaps()
        {
            for (int i = 0; i < WorkManager.activePolicies.Count; i++)
            {
                MapActivePolicy map = WorkManager.activePolicies[i];
                if (!Find.Maps.Any(x => x.uniqueID == map.mapId))
                {
                    if (Find.Maps.Count == 1 && !WorkManager.ActivePoliciesContainsValidMap())
                    {
                        //this means the player was on the move without any base
                        //and just re-settled. So, let's move the settings to
                        //the new map
                        int mapid = Find.CurrentMap.uniqueID;
                        WorkManager.MoveLinksToMap(mapid);
                        map.mapId = mapid;
                    }
                    else
                    {
                        WorkManager.DeleteLinksInMap(map.mapId);
                        WorkManager.DeleteMap(map);
                    }
                }
            }
        }

        internal static void LoadState(
            List<WorkLink> links, List<Pawn> pawns, Policy policy)
        {
            List<WorkLink> mapLinks = null;
            List<WorkLink> zoneLinks = null;
            int currentMap = Find.CurrentMap.uniqueID;

            //get all links from the current map
            mapLinks = links.FindAll(x => x.mapId == currentMap);
            //get all links from the selected zone
            zoneLinks = mapLinks.FindAll(x => x.zone == policy.id);

            foreach (Pawn p in pawns)
            {
                foreach (WorkLink l in zoneLinks)
                {
                    if (l.colonist != null && l.colonist.Equals(p))
                    {
                        WorkManager.LoadPawnPriorities(p, l);
                    }
                }
            }

            WorkManager.SetActivePolicy(policy);
        }


        internal static void SavePawnPriorities(Pawn p, WorkLink link)
        {
            if (link.settings != null)
            {
                foreach (var worktype in DefDatabase<WorkTypeDef>.AllDefsListForReading)
                {
                    if (link.settings.ContainsKey(worktype))
                    {
                        link.settings[worktype] = p.workSettings.GetPriority(worktype);
                    }
                    else
                    {
                        link.settings.Add(worktype, p.workSettings.GetPriority(worktype));
                    }
                }
            }
        }

        internal static void LoadPawnPriorities(Pawn p, WorkLink link)
        {
            if (link.settings != null)
            {
                foreach (KeyValuePair<WorkTypeDef, int> entry in link.settings)
                {
                    p.workSettings.SetPriority(entry.Key, link.settings.TryGetValue(entry.Key));
                }
            }
        }

        internal static void CopyToClipboard()
        {
            Policy policy = GetActivePolicy();
            if (WorkManager.clipboard != null)
            {
                clipboard = new List<WorkLink>();
            }

            WorkManager.clipboard.Clear();
            foreach (WorkLink link in WorkManager.links)
            {
                if (link.zone == policy.id)
                {
                    WorkManager.clipboard.Add(link);
                }
            }               
            
        }

        internal static void PasteToActivePolicy()
        {
            Policy policy = GetActivePolicy();
            if (!WorkManager.clipboard.NullOrEmpty() && WorkManager.clipboard[0].zone != policy.id)
            {            
                WorkManager.links.RemoveAll( x => x.zone == policy.id);
                foreach (WorkLink copiedLink in WorkManager.clipboard)
                {
                    copiedLink.zone = policy.id;
                    WorkManager.links.Add(new WorkLink(copiedLink));
                }
                WorkManager.LoadState(links, Find.CurrentMap.mapPawns.FreeColonists.ToList(), policy);
            }            
        }
    }
}
