namespace PWBFuelBalancer
{
    internal class PartAndResource
    {
        internal Part Part;
        internal PartResource Resource;
        internal double initialTransferRate = 1f;
        internal double fNextAmountMoved = 1f;

        public PartAndResource(Part pPart, PartResource pResource)
        {
            Part = pPart;
            Resource = pResource;
            foreach (var r in pPart.Resources)
                if (r.resourceName == pResource.resourceName)
                {
                    fNextAmountMoved =
                        initialTransferRate = r.maxAmount / 20f / 50f;
                    break;
                }
        }
    }
}
