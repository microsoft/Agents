namespace DispatcherAgent.Model
{
    public class OAuthFlowState
    {
        public bool FlowStarted = false;
        public DateTime FlowExpires = DateTime.MinValue;
    }
}
