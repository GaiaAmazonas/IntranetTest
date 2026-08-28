using Gaia.Modules.Communications;
namespace Gaia.ArchitectureTests;
public sealed class CommunicationsRulesTests
{
 [Fact] public void EventTypeRequiresValidHexColor()=>Assert.Throws<ArgumentException>(()=>CommunicationsRules.Validate(new EventTypeWriteRequest("Capacitación","CAP","blue",null,1,true)));
 [Fact] public void EventEndMustFollowStart(){var now=DateTimeOffset.UtcNow;Assert.Throws<ArgumentException>(()=>CommunicationsRules.Validate(new EventWriteRequest("Encuentro",Guid.NewGuid(),null,null,now,now,false,1,null,null,Guid.NewGuid())));}
 [Theory][InlineData(1,"publish",true)][InlineData(2,"finish",true)][InlineData(3,"publish",true)][InlineData(4,"publish",true)]public void EventTransitionsAreControlled(int state,string action,bool expected)=>Assert.Equal(expected,CommunicationsRules.EventTransition(state,action));
 [Theory][InlineData(1,"design",true)][InlineData(2,"publish",true)][InlineData(3,"close",true)][InlineData(3,"reject",false)]public void BannerTransitionsAreControlled(int state,string action,bool expected)=>Assert.Equal(expected,CommunicationsRules.BannerTransition(state,action));
 [Fact] public void EventDestinationRequiresEvent()=>Assert.Throws<ArgumentException>(()=>CommunicationsRules.Validate(new BannerWriteRequest("Pieza",null,null,"Título",null,DateTimeOffset.UtcNow,null,1,1,null,Guid.NewGuid())));
 [Fact] public void ExternalDestinationRequiresAbsoluteUrl()=>Assert.Throws<ArgumentException>(()=>CommunicationsRules.Validate(new BannerWriteRequest("Pieza",null,null,"Título",null,DateTimeOffset.UtcNow,null,1,2,"relativo",Guid.NewGuid())));
}
