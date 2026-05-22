document.addEventListener("DOMContentLoaded", function () {
    // Busca todos os formulários da página
    const formularios = document.querySelectorAll("form");

    formularios.forEach(form => {
        form.addEventListener("submit", function () {
            // Busca todos os inputs que são do tipo texto ou número que lidam com dinheiro/taxas
            const inputsDecimais = form.querySelectorAll("input[data-decimal='true'], .input-decimal");

            inputsDecimais.forEach(input => {
                // Se o usuário digitou com vírgula (padrão BR), transforma em ponto antes do envio para o C# não quebrar
                if (input.value.includes(",")) {
                    input.value = input.value.replace(/\./g, "").replace(",", ".");
                }
            });
        });
    });
});