using HumanLastRampart.Combat.Battle;
using HumanLastRampart.UI;
using NUnit.Framework;

// W39: 전투 성벽 부서짐 단계(WallDamageRules)가 K-123 결과 화면 요새 그림(UiArt.FortressWall)과 어긋나지 않는지 본다
// 같은 점: 체력 0 = 파공(3단계) = 그림의 broken, 70% 이상 = 멀쩡(0단계) = intact. 다른 점: 경계는 전투 재질 기준(66%·33%)을 유지했다
public class WallDamageStageUiTests
{
    [Test]
    public void BattleStages_AgreeWithResultBoardDiagram_AtTheEnds()
    {
        for (int hp = 0; hp <= 100; hp++)
        {
            float ratio = hp / 100f;
            int stage = WallDamageRules.StageOf(hp, 100);
            string key = UiArt.FortressWall("north", ratio);
            if (hp == 0) { StringAssert.EndsWith("_broken", key); Assert.AreEqual(3, stage); }
            else Assert.AreNotEqual(3, stage, "체력 " + hp + "% 는 파공이 아니다");
            if (key.EndsWith("_intact")) Assert.AreEqual(0, stage, "그림이 멀쩡이면 전투도 멀쩡 (체력 " + hp + "%)");
            if (stage == 0) Assert.That(ratio, Is.GreaterThanOrEqualTo(0.66f));
            if (key.EndsWith("_critical")) Assert.GreaterOrEqual(stage, 1, "그림이 위험이면 전투도 금감 이상 (체력 " + hp + "%)");
        }
    }
}
