using EdiEngine.Common.Enums;
using EdiEngine.Common.Definitions;
using EdiEngine.Standards.X12_004030.Segments;

namespace EdiEngine.Standards.X12_004030.Maps
{
	public class M_436 : MapLoop
	{
		public M_436() : base(null)
		{
			Content.AddRange(new MapBaseEntity[] {
				new LFI() { ReqDes = RequirementDesignator.Mandatory, MaxOccurs = 1 },
				new N7() { ReqDes = RequirementDesignator.Mandatory, MaxOccurs = 1 },
				new K3() { ReqDes = RequirementDesignator.Optional, MaxOccurs = 3 },
			});
		}

	}
 }

