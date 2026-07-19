namespace PogoInventory.Automation.Models;

public enum AutomationActionKind
{
    TapFirstInventoryCard,
    TapDetailsMenu,
    TapAppraise,
    SwipeNextPokemon,
    CaptureEvidence,
    CaptureObservation,
    WaitForState,
    DismissKnownInformationalPopup
    ,OpenPokemonInventory
    ,OpenInventorySearch
    ,ClearInventorySearch
    ,EnterInventorySearchText
    ,ApplyInventorySearch
    ,TapFirstSearchResult
    ,CloseAppraisal
    ,OpenPokemonTagMenu
    ,SelectConfiguredTag
    ,ConfirmConfiguredTag
    ,BackToFilteredInventory
}
