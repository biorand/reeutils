using System;
using System.Collections.Generic;

namespace IntelOrca.Biohazard.REE.Variables.Rsz
{
    /// <summary>
    /// Utility class for RSZ instance operations that are common across multiple components.
    /// </summary>
    internal static class RszInstanceOperations
    {
        public static HashSet<int> FindNestedObjects(
            Dictionary<int, Dictionary<string, object>> parsedElements,
            int instanceId,
            IList<int>? objectTable = null)
        {
            var nestedObjects = new HashSet<int>();
            if (objectTable == null)
                objectTable = Array.Empty<int>();

            var objectTableIds = new HashSet<int>(objectTable);
            int baseObjectIdx = -1;

            for (int i = 0; i < objectTable.Count; i++)
            {
                if (objectTable[i] == instanceId)
                {
                    baseObjectIdx = i;
                    break;
                }
            }

            if (baseObjectIdx <= 0)
                return nestedObjects;

            int prevInstanceId = 0;
            for (int i = baseObjectIdx - 1; i >= 0; i--)
            {
                if (objectTable[i] > 0)
                {
                    prevInstanceId = objectTable[i];
                    break;
                }
            }

            for (int potentialNestedId = prevInstanceId + 1; potentialNestedId < instanceId; potentialNestedId++)
            {
                if (potentialNestedId > 0 && !objectTableIds.Contains(potentialNestedId))
                    nestedObjects.Add(potentialNestedId);
            }

            return nestedObjects;
        }

        public static void FindUserdataReferences(
            Dictionary<string, object> fields,
            HashSet<int> userdataRefs)
        {
            foreach (var fieldData in fields.Values)
            {
                if (fieldData is Data.UserDataData userData && userData.Value > 0)
                {
                    userdataRefs.Add(userData.Value);
                }
                else if (fieldData is Data.ArrayData<Data.UserDataData> arrayData)
                {
                    foreach (var element in arrayData.Values)
                    {
                        if (element is Data.UserDataData arrUserData && arrUserData.Value > 0)
                            userdataRefs.Add(arrUserData.Value);
                    }
                }
                else if (fieldData is IEnumerable<object> genericArray)
                {
                    foreach (var element in genericArray)
                    {
                        if (element is Data.UserDataData arrUserData && arrUserData.Value > 0)
                            userdataRefs.Add(arrUserData.Value);
                    }
                }
            }
        }

        public static void UpdateReferencesBeforeDeletion(
            Dictionary<int, Dictionary<string, object>> parsedElements,
            HashSet<int> deletedIds,
            Dictionary<int, int> idAdjustments)
        {
            foreach (var kvp in parsedElements)
            {
                int instanceId = kvp.Key;
                var fields = kvp.Value;
                if (deletedIds.Contains(instanceId))
                    continue;

                foreach (var fieldData in fields.Values)
                {
                    if (fieldData is Data.ObjectData objData)
                    {
                        int refId = objData.Value;
                        if (refId > 0)
                        {
                            if (deletedIds.Contains(refId))
                                objData.Value = 0;
                            else if (idAdjustments.ContainsKey(refId))
                                objData.Value = idAdjustments[refId];
                        }
                    }
                    else if (fieldData is Data.UserDataData userData)
                    {
                        int refId = userData.Value;
                        if (refId > 0)
                        {
                            if (deletedIds.Contains(refId))
                                userData.Value = 0;
                            else if (idAdjustments.ContainsKey(refId))
                                userData.Value = idAdjustments[refId];
                        }
                    }
                    else if (fieldData is IEnumerable<object> arrayData)
                    {
                        foreach (var element in arrayData)
                        {
                            if (element is Data.ObjectData arrObjData)
                            {
                                int refId = arrObjData.Value;
                                if (refId > 0)
                                {
                                    if (deletedIds.Contains(refId))
                                        arrObjData.Value = 0;
                                    else if (idAdjustments.ContainsKey(refId))
                                        arrObjData.Value = idAdjustments[refId];
                                }
                            }
                            else if (element is Data.UserDataData arrUserData)
                            {
                                int refId = arrUserData.Value;
                                if (refId > 0)
                                {
                                    if (deletedIds.Contains(refId))
                                        arrUserData.Value = 0;
                                    else if (idAdjustments.ContainsKey(refId))
                                        arrUserData.Value = idAdjustments[refId];
                                }
                            }
                        }
                    }
                }
            }
        }

        public static bool IsExclusivelyReferencedFrom(
            Dictionary<int, Dictionary<string, object>> parsedElements,
            int instanceId,
            int sourceId,
            IList<int>? objectTable = null)
        {
            if (instanceId <= 0)
                return false;

            if (objectTable != null && objectTable.Contains(instanceId))
                return false;

            foreach (var kvp in parsedElements)
            {
                int checkId = kvp.Key;
                if (checkId == sourceId)
                    continue;

                var fields = kvp.Value;
                foreach (var fieldData in fields.Values)
                {
                    if (fieldData is Data.ObjectData objData && objData.Value == instanceId)
                        return false;
                    else if (fieldData is Data.UserDataData userData && userData.Value == instanceId)
                        return false;
                    else if (fieldData is IEnumerable<object> arrayData)
                    {
                        foreach (var item in arrayData)
                        {
                            if (item is Data.ObjectData arrObjData && arrObjData.Value == instanceId)
                                return false;
                            else if (item is Data.UserDataData arrUserData && arrUserData.Value == instanceId)
                                return false;
                        }
                    }
                }
            }
            return true;
        }

        public static Dictionary<int, List<(string, string)>> FindAllInstanceReferences(
            Dictionary<int, Dictionary<string, object>> parsedElements,
            int instanceId)
        {
            var references = new Dictionary<int, List<(string, string)>>();

            foreach (var kvp in parsedElements)
            {
                int refId = kvp.Key;
                var fields = kvp.Value;
                foreach (var field in fields)
                {
                    string fieldName = field.Key;
                    var fieldData = field.Value;

                    if (fieldData is Data.ObjectData objData && objData.Value == instanceId)
                    {
                        if (!references.ContainsKey(refId))
                            references[refId] = new List<(string, string)>();
                        references[refId].Add((fieldName, "direct"));
                    }
                    else if (fieldData is Data.UserDataData userData && userData.Value == instanceId)
                    {
                        if (!references.ContainsKey(refId))
                            references[refId] = new List<(string, string)>();
                        references[refId].Add((fieldName, "direct"));
                    }
                    else if (fieldData is IEnumerable<object> arrayData)
                    {
                        int i = 0;
                        foreach (var item in arrayData)
                        {
                            if (item is Data.ObjectData arrObjData && arrObjData.Value == instanceId)
                            {
                                if (!references.ContainsKey(refId))
                                    references[refId] = new List<(string, string)>();
                                references[refId].Add(($"{fieldName}[{i}]", "array_object"));
                            }
                            else if (item is Data.UserDataData arrUserData && arrUserData.Value == instanceId)
                            {
                                if (!references.ContainsKey(refId))
                                    references[refId] = new List<(string, string)>();
                                references[refId].Add(($"{fieldName}[{i}]", "array_object"));
                            }
                            i++;
                        }
                    }
                }
            }
            return references;
        }

        public static HashSet<int> CollectAllNestedObjects(
            Dictionary<int, Dictionary<string, object>> parsedElements,
            int rootInstanceId,
            IList<int>? objectTable = null)
        {
            var nestedObjects = new HashSet<int>();
            var processedIds = new HashSet<int>();
            var objectTableIds = objectTable == null ? new HashSet<int>() : new HashSet<int>(objectTable);
            objectTableIds.Add(0);

            void ExploreInstance(int instanceId)
            {
                if (processedIds.Contains(instanceId))
                    return;
                processedIds.Add(instanceId);

                if (!parsedElements.ContainsKey(instanceId))
                    return;

                var fields = parsedElements[instanceId];

                var positionBasedNested = FindNestedObjects(parsedElements, instanceId, objectTable);

                foreach (var fieldData in fields.Values)
                {
                    if (fieldData is Data.ObjectData objData && objData.Value > 0)
                    {
                        int refId = objData.Value;
                        if (objectTableIds.Contains(refId))
                            continue;
                        if (refId != instanceId && !processedIds.Contains(refId))
                        {
                            nestedObjects.Add(refId);
                            ExploreInstance(refId);
                        }
                    }
                    else if (fieldData is IEnumerable<object> arrayData)
                    {
                        foreach (var element in arrayData)
                        {
                            if (element is Data.ObjectData arrObjData && arrObjData.Value > 0)
                            {
                                int refId = arrObjData.Value;
                                if (objectTableIds.Contains(refId))
                                    continue;
                                if (refId != instanceId && !processedIds.Contains(refId))
                                {
                                    bool isExclusive = IsExclusivelyReferencedFrom(parsedElements, refId, instanceId, objectTable);
                                    if (isExclusive)
                                    {
                                        nestedObjects.Add(refId);
                                        ExploreInstance(refId);
                                    }
                                }
                            }
                        }
                    }
                }

                foreach (var nestedId in positionBasedNested)
                {
                    if (!processedIds.Contains(nestedId) && !objectTableIds.Contains(nestedId))
                    {
                        nestedObjects.Add(nestedId);
                        ExploreInstance(nestedId);
                    }
                }
            }

            ExploreInstance(rootInstanceId);
            return nestedObjects;
        }

        public static HashSet<int> FindObjectReferences(Dictionary<string, object> fields)
        {
            var references = new HashSet<int>();
            foreach (var fieldData in fields.Values)
            {
                if (fieldData is Data.ObjectData objData && objData.Value > 0)
                {
                    references.Add(objData.Value);
                }
                else if (fieldData is IEnumerable<object> arrayData)
                {
                    foreach (var element in arrayData)
                    {
                        if (element is Data.ObjectData arrObjData && arrObjData.Value > 0)
                            references.Add(arrObjData.Value);
                    }
                }
            }
            return references;
        }
    }
}
