using System.Collections;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.InputSystem.EnhancedTouch;
using XDPaint;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    private GameState _gameState;
    public GameState GameState => _gameState;

    [TitleGroup("Paint References")]
    [SerializeField] private PaintManager _paintManager;
    [SerializeField] private MapPainterManager _mapPainterManager;

    [TitleGroup("Battle Settings")]
    [SerializeField] private PainterConfig _upTeamPainterConfig;
    [SerializeField] private PainterConfig _downTeamPainterConfig;

    private BattleTeam _upTeam;
    private BattleTeam _downTeam;

    private float _spawnTimer;

    private bool _spawningForUp;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
    }

    private IEnumerator Start()
    {
        yield return new WaitForSeconds(0.5f);

        EnhancedTouchSupport.Enable();

        Application.targetFrameRate = 60;

        InitializeMatch();
    }

    public void SetGameState(GameState gameState)
    {
        _gameState = gameState;
        switch (_gameState)
        {
            case GameState.InMenu:
                OpenPlayerInteraction();
                break;
            case GameState.Matchmaking:
                DoMatchmaking();
                break;
            case GameState.WaitingForGame:
                InitializeMatch();
                break;
            case GameState.InGame:
                StartMatch();
                break;
            case GameState.Win:
                OnMatchWon();
                break;
            case GameState.Lose:
                OnMatchLose();
                break;
            case GameState.EndGame:
                GoToMenu();
                break;
        }
    }

    public BattleTeam GetUpTeam()
    {
        return _upTeam;
    }
    public BattleTeam GetDownTeam()
    {
        return _downTeam;
    }
    private void OpenPlayerInteraction()
    {
        // TODO: Menu'de varsa yenilik interaction vs. onları burada yükle ve getir.
        // TODO: Oyuncunun interact edebileceği her şeyin interaction'ını aç.
    }

    private void DoMatchmaking()
    {
        // TODO: Başlangıçta aramakta olan bir büyüteç ve boş bir profil olacak.
        // TODO: Random bir enayi profili getirip oyunu başlatıcaz.
    }
    private void InitializeMatch()
    {
        // TODO: Haritanın yarısını boya.
        // _mapPainterManager.PaintTheHalf(MapPainterManager.MovementDirection.TopToBottom);

        var upTeamMinBound = new Vector3(_mapPainterManager.TopRightBound.x, _mapPainterManager.TopRightBound.y, _mapPainterManager.TopRightBound.z);
        var upTeamMaxBound = new Vector3(_mapPainterManager.BottomLeftBound.x, _mapPainterManager.BottomLeftBound.y, (_mapPainterManager.TopRightBound.z + _mapPainterManager.BottomLeftBound.z) / 2f);
        _upTeam = new BattleTeam(_paintManager, _upTeamPainterConfig, Vector3.back, upTeamMinBound, upTeamMaxBound);

        var downTeamMinBound = new Vector3(_mapPainterManager.BottomLeftBound.x, _mapPainterManager.BottomLeftBound.y, _mapPainterManager.BottomLeftBound.z);
        var downTeamMaxBound = new Vector3(_mapPainterManager.TopRightBound.x, _mapPainterManager.TopRightBound.y, (_mapPainterManager.TopRightBound.z + _mapPainterManager.BottomLeftBound.z) / 2f);
        _downTeam = new BattleTeam(_paintManager, _downTeamPainterConfig, Vector3.forward, downTeamMinBound, downTeamMaxBound);

        SetGameState(GameState.InGame);
    }
    private void StartMatch()
    {
        // TODO: Unit spawn'ına başla
        // TODO: Kamera geçişi / geri sayım vs. başlat.
        // TODO: Oyuncu kontrollerini aktif et.
    }
    private void OnMatchWon()
    {
        // TODO: Haritanın boyanmış tüm her yerini temizle
        // TODO: Win paneli getir.
    }
    private void OnMatchLose()
    {
        // TODO: Haritanın tüm her yerini yukarıdan aşağıya boya.
        // TODO: Lose paneli getir.
    }
    private void GoToMenu()
    {
        // TODO: Açık olan panelleri kapat.
        // TODO: Siyah ekran gelsin.
        // TODO: Menü sahnesini yükle.

        // TODO: InMenu State'ine geç
    }
}

public enum GameState
{
    InMenu,
    Matchmaking,
    WaitingForGame,
    InGame,
    Win,
    Lose,
    EndGame
}
