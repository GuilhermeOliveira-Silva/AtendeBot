USE atendebot;

INSERT INTO comercios (nome, telefone, horario_abertura, horario_fechamento)
VALUES ('Pizzaria Teste', '35999999999', '18:00', '23:00');

INSERT INTO cardapio_itens (comercio_id, nome, descricao, preco, categoria) VALUES
(1, 'Pizza Calabresa', 'Calabresa, cebola e azeitona', 35.00, 'Pizza'),
(1, 'Pizza Margherita', 'Tomate, manjericão e mussarela', 32.00, 'Pizza'),
(1, 'Pizza Frango', 'Frango desfiado com catupiry', 38.00, 'Pizza'),
(1, 'Coca-Cola Lata', '350ml', 6.00, 'Bebida'),
(1, 'Suco Natural', 'Laranja 500ml', 8.00, 'Bebida');