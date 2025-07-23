// Variáveis globais para gerenciar a popup
let blingPopup = null;
let authCode = null;
let authPromise = null;

// Função para abrir a popup e aguardar o código
window.waitForBlingAuth = function() {
    return new Promise((resolve, reject) => {
        authPromise = { resolve, reject };
        
        // Verificar se a popup foi fechada
        const checkClosed = setInterval(() => {
            if (blingPopup && blingPopup.closed) {
                clearInterval(checkClosed);
                if (authCode) {
                    resolve(authCode);
                } else {
                    reject(new Error("Popup fechada sem código de autorização"));
                }
            }
        }, 1000);
        
        // Timeout após 5 minutos
        setTimeout(() => {
            clearInterval(checkClosed);
            if (blingPopup && !blingPopup.closed) {
                blingPopup.close();
            }
            reject(new Error("Timeout na autorização"));
        }, 300000);
    });
};

// Função para capturar o código da popup
window.captureBlingCode = function() {
    if (authCode) {
        const code = authCode;
        authCode = null;
        return code;
    }
    return null;
};

// Função para verificar se há código na URL atual (chamada pela popup)
window.checkForAuthCode = function() {
    const urlParams = new URLSearchParams(window.location.search);
    const code = urlParams.get('code');
    const state = urlParams.get('state');
    
    if (code && state) {
        // Verificar se o state corresponde ao esperado
        if (state === '53a7fb14d9f453aa0dfd8eb1322a65a1') {
            authCode = code;
            
            // Atualizar status na página
            const statusElement = document.getElementById('status');
            if (statusElement) {
                statusElement.innerHTML = '<div class="alert alert-success">Autorização bem-sucedida! Fechando popup...</div>';
            }
            
            // Fechar a popup após 2 segundos
            setTimeout(() => {
                if (window.opener) {
                    window.close();
                }
            }, 2000);
            
            return true;
        } else {
            const statusElement = document.getElementById('status');
            if (statusElement) {
                statusElement.innerHTML = '<div class="alert alert-danger">Erro: State inválido</div>';
            }
        }
    } else {
        const statusElement = document.getElementById('status');
        if (statusElement) {
            statusElement.innerHTML = '<div class="alert alert-warning">Aguardando autorização...</div>';
        }
    }
    
    return false;
};

// Função para capturar código de qualquer URL (incluindo httpbin)
window.captureCodeFromAnyUrl = function() {
    const urlParams = new URLSearchParams(window.location.search);
    const code = urlParams.get('code');
    const state = urlParams.get('state');
    
    if (code && state && state === '53a7fb14d9f453aa0dfd8eb1322a65a1') {
        // Enviar o código para a janela pai
        if (window.opener) {
            window.opener.postMessage({ type: 'BLING_AUTH_CODE', code: code }, '*');
        }
        
        // Mostrar mensagem de sucesso
        document.body.innerHTML = `
            <div style="text-align: center; padding: 50px; font-family: Arial, sans-serif;">
                <h3 style="color: green;">✅ Autorização Concluída!</h3>
                <p>Código capturado: <strong>${code}</strong></p>
                <p>Esta janela será fechada automaticamente...</p>
            </div>
        `;
        
        // Fechar após 3 segundos
        setTimeout(() => {
            window.close();
        }, 3000);
        
        return true;
    }
    
    return false;
};

// Listener para mensagens da popup
window.addEventListener('message', function(event) {
    if (event.data.type === 'BLING_AUTH_CODE') {
        authCode = event.data.code;
        if (authPromise && authPromise.resolve) {
            authPromise.resolve(authCode);
        }
    }
});

// Executar verificação quando a página carrega
document.addEventListener('DOMContentLoaded', function() {
    // Tentar capturar código de qualquer URL
    if (window.captureCodeFromAnyUrl()) {
        console.log('Código de autorização capturado via URL');
    } else if (window.checkForAuthCode()) {
        console.log('Código de autorização capturado via callback');
    }
}); 