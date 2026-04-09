using System.Collections.Generic;
using System.Linq;
using UnityEngine;
[CreateAssetMenu(fileName = "所有职业的数据", menuName = "Data/所有职业的数据")]
public class PlayerCharacterStateDataSO : ScriptableObject
{
   public List<PlayerCharacterStateBaseData> AllPlayerCharacterStateBaseData;
   
   public PlayerCharacterStateBaseData GetPlayerCharacterStateBaseData(CharacterProfession profession )
   {
       return AllPlayerCharacterStateBaseData.First(x => x.profession == profession);
   }
}
