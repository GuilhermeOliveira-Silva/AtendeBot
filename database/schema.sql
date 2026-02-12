CREATE DATABASE IF NOT EXISTS atendebot;
USE atendebot;

CREATE TABLE comercios (
    id INT PRIMARY KEY AUTO_INCREMENT,
    nome VARCHAR(100) NOT NULL,
    telefone VARCHAR(20),
    telegram_chat_id VARCHAR(50),
    horario_abertura TIME,
    horario_fechamento TIME,
    ativo BOOLEAN DEFAULT TRUE
);

CREATE TABLE cardapio_itens (
    id INT PRIMARY KEY AUTO_INCREMENT,
    comercio_id INT NOT NULL,
    nome VARCHAR(100) NOT NULL,
    descricao VARCHAR(255),
    preco DECIMAL(10,2) NOT NULL,
    categoria VARCHAR(50),
    disponivel BOOLEAN DEFAULT TRUE,
    FOREIGN KEY (comercio_id) REFERENCES comercios(id)
);

CREATE TABLE pedidos (
    id INT PRIMARY KEY AUTO_INCREMENT,
    comercio_id INT NOT NULL,
    cliente_nome VARCHAR(100),
    cliente_telegram_id VARCHAR(50),
    tipo_entrega ENUM('retirada', 'entrega'),
    endereco_entrega VARCHAR(255),
    status ENUM('novo', 'preparando', 'saiu_entrega', 'entregue', 'cancelado') DEFAULT 'novo',
    total DECIMAL(10,2),
    criado_em DATETIME DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (comercio_id) REFERENCES comercios(id)
);

CREATE TABLE pedido_itens (
    id INT PRIMARY KEY AUTO_INCREMENT,
    pedido_id INT NOT NULL,
    cardapio_item_id INT NOT NULL,
    quantidade INT NOT NULL,
    preco_unitario DECIMAL(10,2) NOT NULL,
    FOREIGN KEY (pedido_id) REFERENCES pedidos(id),
    FOREIGN KEY (cardapio_item_id) REFERENCES cardapio_itens(id)
);