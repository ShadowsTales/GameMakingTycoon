using UnityEngine;
using UnityEngine.UIElements;

public class BaukastenManager : MonoBehaviour 
{
    private NodeBoardView _boardView;
    private VisualElement _gameCore;
    private GameCreatorController _logic;

    public void Init(VisualElement root, GameCreatorController controller)
    {
        _logic = controller;
        //_boardView = new NodeBoardView(root.Q<VisualElement>("DevelopmentBoard"));
        
        // GameCore mittig im Board-Content platzieren
        _gameCore = root.Q<VisualElement>("GameCore"); 
        _boardView.GetSpawnArea().Add(_gameCore); 

        SpawnNodes();
    }

    private void SpawnNodes()
    {
        foreach(var feature in _logic.featureDB.allFeatures)
        {
            // Erstelle Node (Nutze USS Klassen!)
            var node = new VisualElement();
            node.AddToClassList("feature-node"); 
            node.style.position = Position.Absolute;
            
            // Setze zufällige Position oder Ast-Position
            node.style.left = 2500 + Random.Range(-300, 300);
            node.style.top = 2500 + Random.Range(-300, 300);

            // Rechtsklick für Info
            node.RegisterCallback<ContextualMenuPopulateEvent>(evt => {
                evt.menu.AppendAction("Details anzeigen", (a) => ShowDetails(feature));
            });

            _boardView.GetSpawnArea().Add(node);
        }
    }

    void ShowDetails(FeatureSO f) {
        // Logik für Info-Panel in der Ecke
    }
}