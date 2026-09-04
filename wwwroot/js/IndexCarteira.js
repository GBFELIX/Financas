function calcularNovoPrecoMedio(id, qtdAtual, pmAtual, precoAtual) {
    const inputQtd = document.getElementById(`inputQtdCompra-${id}`);
    const txtAporte = document.getElementById(`txtNovoAporte-${id}`);
    const txtNovoPM = document.getElementById(`txtNovoPM-${id}`);

    if (!inputQtd) return;

    const qtdComprada = parseFloat(inputQtd.value) || 0;

    const custoAporte = qtdComprada * precoAtual;
    txtAporte.innerText = custoAporte.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' });

    const custoTotalAcumulado = (qtdAtual * pmAtual) + custoAporte;
    const totalAcoesNovas = qtdAtual + qtdComprada;

    const novoPrecoMedioEstimado = totalAcoesNovas > 0 ? (custoTotalAcumulado / totalAcoesNovas) : 0;
    txtNovoPM.innerText = novoPrecoMedioEstimado.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' });
}

document.addEventListener("DOMContentLoaded", function () {
    @foreach(var item in Model.Itens.Where(x => x.TipoAtivo != "RendaFixa"))
{
    <text>calcularNovoPrecoMedio('@item.Id', @item.Quantidade, @item.PrecoMedio.ToString("F2", System.Globalization.CultureInfo.InvariantCulture), @item.PrecoAtual.ToString("F2", System.Globalization.CultureInfo.InvariantCulture));</text>
}
    });

document.addEventListener("DOMContentLoaded", function () {
    const modalSelect = document.getElementById('selectModalTicker');
    if (modalSelect) {
        new TomSelect(modalSelect, {
            plugins: ['dropdown_input'],
            create: false,
            sortField: { field: "text", direction: "asc" },
            maxOptions: 60,
            render: {
                option: function (data, escape) {
                    return '<div class="option py-2">' + escape(data.text) + '</div>';
                },
                item: function (data, escape) {
                    return '<div class="item fw-bold text-success">' + escape(data.text) + '</div>';
                }
            }
        });
    }
});

function tornarRendimentoEditavel(id, valorAtual) {
    const celula = document.getElementById(`celula-rendimento-${id}`);

    if (celula.querySelector('input')) return;

    const input = document.createElement('input');
    input.type = 'number';
    input.step = '0.01';
    input.min = '0.00';
    input.className = 'form-control form-control-sm text-success fw-bold text-center border-primary d-inline-block';
    input.style.maxWidth = '90px';
    input.value = valorAtual;

    const spanTexto = document.getElementById(`texto-rendimento-${id}`);
    spanTexto.style.display = 'none';
    celula.appendChild(input);
    input.focus();
    input.select();

    const salvarMudanca = () => {
        const novoValor = parseFloat(input.value) || 0;

        fetch(`/Carteiras/AtualizarRendimentoInline?id=${id}&novoValor=${novoValor}`, {
            method: 'POST',
            headers: {
                'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]').value
            }
        })
            .then(response => {
                if (response.ok) {
                    input.remove();
                    spanTexto.textContent = novoValor.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' });
                    spanTexto.style.display = '';
                    celula.setAttribute('onclick', `tornarRendimentoEditavel(${id}, ${novoValor})`);

                    const campoProvento = document.getElementById(`provento-estimado-${id}`);
                    const campoQuantidade = document.getElementById(`quantidade-${id}`);

                    if (campoProvento && campoQuantidade) {
                        const qtdTexto = campoQuantidade.textContent.trim().replace(/\./g, '').replace(',', '.');
                        const quantidade = parseFloat(qtdTexto) || 0;
                        const novoProventoEstimado = quantidade * novoValor;

                        if (novoProventoEstimado > 0) {
                            campoProvento.textContent = novoProventoEstimado.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' });
                        } else {
                            campoProvento.textContent = "-";
                        }
                    }
                } else {
                    alert('Erro ao salvar o novo valor de rendimento.');
                    cancelarEdicao();
                }
            })
            .catch(error => {
                console.error('Erro:', error);
                cancelarEdicao();
            });
    };

    const cancelarEdicao = () => {
        input.remove();
        spanTexto.style.display = '';
    };

    input.addEventListener('keydown', function (e) {
        if (e.key === 'Enter') {
            e.preventDefault();
            salvarMudanca();
        }
        if (e.key === 'Escape') {
            cancelarEdicao();
        }
    });

    input.addEventListener('blur', function () {
        salvarMudanca();
    });
}

document.addEventListener("DOMContentLoaded", function () {
    const modalElement = document.getElementById('modalRendimentosERebalanceamento');
    let chartEvolucao = null;

    modalElement.addEventListener('shown.bs.modal', function () {
        const canvasEvolucao = document.getElementById('chartEvolucaoPatrimonial');
        if (!canvasEvolucao) return;

        let labelsEvolucao = @Html.Raw(Json.Serialize(ViewBag.HistoricoEvolucaoLabels)) || [];
        let dadosEvolucao = @Html.Raw(Json.Serialize(ViewBag.HistoricoEvolucaoValores)) || [];

        // Tratamento preventivo para o primeiro mês do sistema (gráfico de 1 ponto só)
        if (labelsEvolucao.length === 1) {
            labelsEvolucao = ['Início', labelsEvolucao[0]];
            dadosEvolucao = [dadosEvolucao[0], dadosEvolucao[0]];
        }
        else if (labelsEvolucao.length === 0) {
            labelsEvolucao = ['Início', new Date().toLocaleDateString('pt-BR', { month: '2-digit', year: 'numeric' })];
            dadosEvolucao = [@Model.PatrimonioTotalReal, @Model.PatrimonioTotalReal];
        }

        const ctxEvolucao = canvasEvolucao.getContext('2d');

        if (chartEvolucao) {
            chartEvolucao.destroy();
        }

        chartEvolucao = new Chart(ctxEvolucao, {
            type: 'line',
            data: {
                labels: labelsEvolucao,
                datasets: [{
                    label: 'Patrimônio Líquido (R$)',
                    data: dadosEvolucao,
                    borderColor: '#0d6efd',
                    backgroundColor: 'rgba(13, 110, 253, 0.1)',
                    fill: true,
                    tension: 0.3,
                    borderWidth: 3,
                    pointBackgroundColor: '#0d6efd',
                    pointRadius: 4
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    legend: { display: false },
                    tooltip: {
                        callbacks: {
                            label: (context) => 'Saldo: ' + new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' }).format(context.raw)
                        }
                    }
                },
                scales: {
                    y: {
                        beginAtZero: false,
                        ticks: { callback: (v) => 'R$ ' + v.toLocaleString('pt-BR') }
                    }
                }
            }
        });
    });
});

function filtrarTickersModal() {
    const input = document.getElementById('inputFiltroTicker');
    const filtro = input.value.toUpperCase();
    const select = document.getElementById('selectModalTicker');
    const opcoes = select.getElementsByTagName('option');
    const grupos = select.getElementsByTagName('optgroup');

    // Loop por todas as opções para esconder as que não batem com o texto digitado
    for (let i = 0; i < opcoes.length; i++) {
        // Ignora o placeholder inicial
        if (opcoes[i].id === 'optPlaceholderSelecione') continue;

        const textoBusca = opcoes[i].getAttribute('data-search') || '';

        if (textoBusca.includes(filtro)) {
            opcoes[i].style.display = ""; // Mostra
        } else {
            opcoes[i].style.display = "none"; // Esconde
        }
    }

    // Ajuste visual extra: Esconde grupos inteiros se todas as opções de dentro dele sumirem
    for (let g = 0; g < grupos.length; g++) {
        const opcoesDoGrupo = grupos[g].getElementsByTagName('option');
        let algumaOpcaoVisivel = false;

        for (let j = 0; j < opcoesDoGrupo.length; j++) {
            if (opcoesDoGrupo[j].style.display !== "none") {
                algumaOpcaoVisivel = true;
                break;
            }
        }

        if (algumaOpcaoVisivel) {
            grupos[g].style.display = "";
        } else {
            grupos[g].style.display = "none";
        }
    }
}

// Limpa o campo de busca automaticamente quando o utilizador fechar e abrir o modal de novo
document.getElementById('modalAdicionarAtivo').addEventListener('hidden.bs.modal', function () {
    document.getElementById('inputFiltroTicker').value = '';
    filtrarTickersModal();
});

// Função auxiliar para transição fluida entre modais
function abrirConfiguracaoMetasRapido() {
    const modalDetalhes = bootstrap.Modal.getInstance(document.getElementById('modalRendimentosERebalanceamento'));
    const modalDetalhes1 = bootstrap.Modal.getInstance(document.getElementById('modalDiagnosticoAlocacao'));
    if (modalDetalhes) modalDetalhes.hide();
    if (modalDetalhes1) modalDetalhes1.hide();

    setTimeout(() => {
        const modalConfigMetas = new bootstrap.Modal(document.getElementById('modalMetas'));
        modalConfigMetas.show();
    }, 400);
}
document.addEventListener("DOMContentLoaded", function () {
    const selectTicker = document.getElementById("selectModalTicker");
    const inputPrecoMedio = document.querySelector("input[name='precoMedio']");

    if (selectTicker && inputPrecoMedio) {
        selectTicker.addEventListener("change", function () {
            // Pega a opção que o usuário acabou de clicar
            const opcaoSelecionada = this.options[this.selectedIndex];

            // Extrai o valor do data-preco
            let precoAtual = opcaoSelecionada.getAttribute("data-preco");

            if (precoAtual) {
                // Substitui ponto por vírgula para manter o padrão brasileiro no input (Ex: 34.50 vira 34,50)
                precoAtual = precoAtual.replace('.', ',');

                // Preenche o input
                inputPrecoMedio.value = precoAtual;

                // Dispara um evento de input caso você use alguma máscara JS (ex: jQuery Mask) para ele formatar na hora
                inputPrecoMedio.dispatchEvent(new Event('input'));
            } else {
                // Limpa o campo caso o ativo não tenha preço cadastrado
                inputPrecoMedio.value = "";
            }
        });
    }
});

async function alternarFavorito(id, elementoTd) {
    try {
        // Desativa cliques duplos rápidos enquanto processa
        elementoTd.style.pointerEvents = 'none';

        const resposta = await fetch('/Carteiras/AlternarFavorito', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/x-www-form-urlencoded'
            },
            body: `id=${id}`
        });

        if (resposta.ok) {
            const dados = await resposta.json();

            if (dados.sucesso) {
                const iconeEstrela = document.getElementById(`icone-star-${id}`);

                if (dados.favorito) {
                    // Ficou Favorito: Fica Amarelo (text-warning)
                    elementoTd.classList.remove("text-primary");
                    elementoTd.classList.add("text-warning");

                    if (iconeEstrela) {
                        iconeEstrela.className = "bi bi-star-fill me-1";
                    }
                } else {
                    // Removeu Favorito: Volta ao Azul (text-primary)
                    elementoTd.classList.remove("text-warning");
                    elementoTd.classList.add("text-primary");

                    if (iconeEstrela) {
                        iconeEstrela.className = "bi bi-star text-muted opacity-50 me-1";
                    }
                }
            } else {
                console.error(dados.mensagem);
            }
        }
    } catch (erro) {
        console.error("Erro ao favoritar o ativo:", erro);
    } finally {
        // Libera o clique novamente
        elementoTd.style.pointerEvents = 'auto';
    }
}
let carrinho = [];

function adicionarAoCarrinho(id, ticker, preco) {
    let inputQtd = document.getElementById(`inputQtdCompra-${id}`);
    let qtd = parseInt(inputQtd.value);

    if (qtd <= 0) return;

    // Verifica se já existe no carrinho e apenas soma a quantidade
    let itemExistente = carrinho.find(i => i.id === id);
    if (itemExistente) {
        itemExistente.quantidade += qtd;
    } else {
        carrinho.push({ id: id, ticker: ticker, preco: preco, quantidade: qtd });
    }

    atualizarInterfaceCarrinho();

    // Fecha o modal do ativo
    let modalEl = document.getElementById(`modalCompra-${id}`);
    let modalObj = bootstrap.Modal.getInstance(modalEl);
    if (modalObj) modalObj.hide();
}

function removerDoCarrinho(id) {
    carrinho = carrinho.filter(i => i.id !== id);
    atualizarInterfaceCarrinho();
}

function atualizarInterfaceCarrinho() {
    let tbody = document.getElementById('tabelaCarrinhoBody');
    tbody.innerHTML = '';
    let total = 0;
    let qtdItens = 0;

    carrinho.forEach(item => {
        let subtotal = item.quantidade * item.preco;
        total += subtotal;
        qtdItens += item.quantidade;

        tbody.innerHTML += `
                    <tr>
                        <td class="ps-4 fw-bold align-middle">${item.ticker}</td>
                        <td class="text-center align-middle">${item.quantidade}</td>
                        <td class="text-end pe-4 align-middle text-secondary">${subtotal.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' })}</td>
                        <td class="text-center align-middle">
                            <button class="btn btn-sm btn-outline-danger border-0" onclick="removerDoCarrinho(${item.id})">
                                <i class="bi bi-trash"></i>
                            </button>
                        </td>
                    </tr>
                `;
    });

    if (carrinho.length === 0) {
        tbody.innerHTML = `<tr><td colspan="4" class="text-center py-4 text-muted">Seu carrinho está vazio.</td></tr>`;
    }

    let totalFormatado = total.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' });

    // Atualiza o botão do cabeçalho
    document.getElementById('cartCount').innerText = qtdItens;
    document.getElementById('cartTotalHeader').innerText = totalFormatado;

    // Atualiza o total dentro do modal do carrinho
    document.getElementById('cartTotalFinal').innerText = totalFormatado;
}

function realizarComprasEmLote(visaoAtual) {
    if (carrinho.length === 0) return alert("Adicione ativos antes de comprar!");

    fetch(`/Carteiras/ProcessarCarrinho?visao=${visaoAtual}`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(carrinho)
    })
        .then(response => {
            if (response.ok) {
                carrinho = [];
                window.location.reload();
            } else {
                alert("Erro ao processar as compras.");
            }
        });
}