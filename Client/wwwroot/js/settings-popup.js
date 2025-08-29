// Função para adicionar listener de clique fora do popup
window.addClickOutsideListener = function (dotNetHelper) {
    document.addEventListener('click', function (event) {
        // Verifica se o popup está aberto
        const popup = document.querySelector('.settings-popup');
        const gearBtn = document.querySelector('.gear-btn');
        
        if (popup && popup.style.display !== 'none') {
            // Verifica se o clique foi fora do popup e fora do botão da engrenagem
            if (!popup.contains(event.target) && !gearBtn.contains(event.target)) {
                // Fecha o popup
                dotNetHelper.invokeMethodAsync('CloseSettings');
            }
        }
    });
};
