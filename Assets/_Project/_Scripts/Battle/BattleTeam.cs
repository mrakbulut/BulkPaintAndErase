using UnityEngine;
using XDPaint;


public class BattleTeam
{
    private readonly PaintManager _paintManager;
    private readonly PainterConfig _painterConfig;
    private readonly Vector3 _direction;
    private readonly Vector3 _min;
    private readonly Vector3 _max;

    public BattleTeam(PaintManager paintManager, PainterConfig painterConfig, Vector3 direction, Vector3 min, Vector3 max)
    {
        _paintManager = paintManager;
        _painterConfig = painterConfig;
        _direction = direction;
        _min = min;
        _max = max;

        // Debug.Log("DIRECTION : " + direction + ", MIN : " + min + ", MAX : " + max);
    }

    public void SetupUnit(SimpleUnit unit)
    {
        var lookPoint = Quaternion.LookRotation(_direction);
        unit.transform.rotation = Quaternion.Euler(0f, lookPoint.eulerAngles.y, 0f);

        unit.Setup(_paintManager, _painterConfig);
    }

    public Vector3 GetRandomPositionBetweenBounds()
    {
        float randomX = Random.Range(_min.x, _max.x);
        float randomY = Random.Range(_min.y, _max.y);
        float randomZ = Random.Range(_min.z, _max.z);

        return new Vector3(randomX, randomY, randomZ);
    }

    public Quaternion GetTeamRotation()
    {
        var lookPoint = Quaternion.LookRotation(_direction);
        return Quaternion.Euler(0f, lookPoint.eulerAngles.y, 0f);
    }
}
