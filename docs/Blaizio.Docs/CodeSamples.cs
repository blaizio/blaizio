namespace Blaizio.Docs;

/// <summary>One short, realistic file per highlighted language, for the Code Block's Languages demo.</summary>
/// <param name="Language">The <c>Language</c> value that highlights it.</param>
/// <param name="Name">What the language picker shows.</param>
/// <param name="File">The file name in the block's header.</param>
/// <param name="Code">The file's text.</param>
public sealed record CodeSample(string Language, string Name, string File, string Code);

public static class CodeSamples
{
    /// <summary>The 19 languages built into Blaizio.Syntax.</summary>
    public static readonly IReadOnlyList<CodeSample> BuiltIn =
    [
        new("csharp", "C#", "OrderService.cs", """
            // Loads an order with its lines, cached for a minute.
            public sealed class OrderService(ShopDb db, IMemoryCache cache) : IOrderService
            {
                public async Task<Order?> FindAsync(int id, CancellationToken token = default)
                {
                    if (cache.TryGetValue(id, out Order? cached)) return cached;
                    var order = await db.Orders.Include(o => o.Lines).FirstOrDefaultAsync(o => o.Id == id, token);
                    cache.Set(id, order, TimeSpan.FromMinutes(1));
                    return order is { Lines.Count: > 0 } ? order : null;
                }
            }
            """),
        new("razor", "Razor", "Counter.razor", """
            @page "/counter"
            @inject ILogger<Counter> Logger

            <PageTitle>Counter</PageTitle>

            <p role="status">Current count: @_count</p>
            <BzButton OnClick="Increment">Click me</BzButton>

            @code {
                private int _count;

                private void Increment() => Logger.LogInformation("Count is {Count}", ++_count);
            }
            """),
        new("json", "JSON", "appsettings.json", """
            {
              // Logging levels per category
              "Logging": {
                "LogLevel": { "Default": "Information", "Microsoft.AspNetCore": "Warning" }
              },
              "ConnectionStrings": { "Shop": "Host=localhost;Database=shop" },
              "Features": { "Checkout": true, "MaxCartItems": 50, "Banner": null }
            }
            """),
        new("bash", "Bash", "deploy.sh", """
            #!/usr/bin/env bash
            set -euo pipefail
            # Publish, then copy the build to the server
            dotnet publish src/Shop.Web -c Release -o ./publish
            if [[ -n "${DEPLOY_HOST:-}" ]]; then
              rsync -az --delete ./publish/ "deploy@$DEPLOY_HOST:/srv/shop"
            fi
            echo "Published $(git rev-parse --short HEAD)"
            """),
        new("console", "Console", "Terminal", """
            $ dotnet new blazor -o Shop
            The template "Blazor Web App" was created successfully.
            $ cd Shop
            $ blaizio add button card --style aura
            Added 4 item(s).
            # The same app from PowerShell on Windows
            PS C:\src\Shop> dotnet watch --project Shop.csproj
            """),
        new("powershell", "PowerShell", "Pack.ps1", """
            param([string] $Configuration = 'Release')
            # Pack every project, then list the packages
            dotnet pack -c $Configuration -o ./artifacts
            $packages = Get-ChildItem ./artifacts -Filter *.nupkg
            foreach ($package in $packages) {
                Write-Host "$($package.Name): $([Math]::Round($package.Length / 1kb)) KB"
            }
            if ($packages.Count -eq 0) { throw 'No packages were created.' }
            """),
        new("typescript", "TypeScript", "resize.ts", """
            import type { DotNetObject } from "./interop";

            // Reports an element's size to .NET whenever it changes.
            export function observeSize(element: HTMLElement, dotNet: DotNetObject): () => void {
              const observer = new ResizeObserver(([entry]) => {
                const { width, height } = entry.contentRect;
                void dotNet.invokeMethodAsync("OnResize", Math.round(width), Math.round(height));
              });
              observer.observe(element);
              return () => observer.disconnect();
            }
            """),
        new("javascript", "JavaScript", "theme.js", """
            // Applies the saved theme before the first paint.
            const saved = localStorage.getItem("theme");
            const dark = saved ? saved === "dark" : matchMedia("(prefers-color-scheme: dark)").matches;
            document.documentElement.classList.toggle("dark", dark);

            export function setTheme(theme) {
              localStorage.setItem("theme", theme);
              document.documentElement.classList.toggle("dark", theme === "dark");
            }
            """),
        new("html", "HTML", "index.html", """
            <!DOCTYPE html>
            <html lang="en">
            <head>
              <meta charset="utf-8" />
              <title>Shop</title>
              <link rel="stylesheet" href="app.css" />
            </head>
            <body>
              <div id="app">Loading&hellip;</div>
              <script src="_framework/blazor.webassembly.js"></script>
            </body>
            </html>
            """),
        new("xml", "XML", "Shop.Web.csproj", """
            <Project Sdk="Microsoft.NET.Sdk.Web">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <Nullable>enable</Nullable>
              </PropertyGroup>
              <!-- The components' headless primitives -->
              <ItemGroup>
                <PackageReference Include="Blaizio.Base" Version="0.3.6" />
              </ItemGroup>
            </Project>
            """),
        new("css", "CSS", "app.css", """
            @import "tailwindcss";

            /* Order cards lift on hover */
            .order-card {
              padding: 1rem 1.5rem;
              border: 1px solid var(--border);
              transition: box-shadow 150ms ease-out;

              &:hover {
                box-shadow: 0 4px 12px rgb(0 0 0 / 8%);
              }
            }
            """),
        new("sql", "SQL", "TopCustomers.sql", """
            -- Customers with the most revenue this year
            SELECT TOP (10) c.Id, c.Name, SUM(o.Total) AS Revenue
            FROM dbo.Customers AS c
            INNER JOIN dbo.Orders AS o ON o.CustomerId = c.Id
            WHERE o.PlacedAt >= @YearStart AND o.Status <> 'Cancelled'
            GROUP BY c.Id, c.Name
            HAVING COUNT(*) > 2
            ORDER BY Revenue DESC;
            """),
        new("yaml", "YAML", "build.yml", """
            name: Build
            on:
              push:
                branches: [main]
            jobs:
              test:
                runs-on: ubuntu-latest
                steps:
                  - uses: actions/checkout@v4
                  - name: Test
                    run: dotnet test --configuration Release
                    env:
                      DOTNET_NOLOGO: true
            """),
        new("diff", "Diff", "cart-total.patch", """
            diff --git a/src/Cart.cs b/src/Cart.cs
            --- a/src/Cart.cs
            +++ b/src/Cart.cs
            @@ -12,2 +12,2 @@ public sealed class Cart
                 public decimal Total =>
            -        _items.Sum(i => i.Price);
            +        _items.Sum(i => i.Price * i.Quantity);
            """),
        new("http", "HTTP", "orders.http", """
            @host = https://localhost:7043

            ### List orders
            GET {{host}}/api/orders?page=1
            Accept: application/json

            ### Place an order
            POST {{host}}/api/orders
            Content-Type: application/json

            {
              "customerId": 42,
              "items": [{ "sku": "MUG-01", "quantity": 2 }]
            }
            """),
        new("dockerfile", "Dockerfile", "Dockerfile", """
            FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
            WORKDIR /src
            COPY . .
            RUN dotnet publish src/Shop.Web -c Release -o /app

            FROM mcr.microsoft.com/dotnet/aspnet:10.0
            WORKDIR /app
            COPY --from=build /app .
            ENV ASPNETCORE_URLS=http://+:8080
            EXPOSE 8080
            ENTRYPOINT ["dotnet", "Shop.Web.dll"]
            """),
        new("ini", "INI", ".editorconfig", """
            root = true

            [*.{cs,razor}]
            indent_style = space
            indent_size = 4
            csharp_style_namespace_declarations = file_scoped:warning

            # JSON stays compact
            [*.json]
            indent_size = 2
            """),
        new("toml", "TOML", "deploy.toml", """
            # Deployment settings
            name = "shop"
            region = "eu-west"

            [build]
            image = "shop-web:latest"
            args = ["--configuration", "Release"]

            [[services]]
            port = 8080
            health = { path = "/health", interval = "30s" }
            """),
        new("markdown", "Markdown", "README.md", """
            # Shop

            A **Blazor** storefront with *server-side* rendering.

            ## Getting started

            1. Install the CLI: `dotnet tool install -g Blaizio.Cli`
            2. Run the app and open the [local site](https://localhost:7043).

            ```bash
            dotnet watch --project src/Shop.Web
            ```
            """),
    ];

    /// <summary>The languages of Blaizio.Syntax.Languages, which the docs add with <c>options.Syntax.AddAll()</c>.</summary>
    public static readonly IReadOnlyList<CodeSample> Extra =
    [
        new("python", "Python", "orders.py", """"
            from dataclasses import dataclass
            import re

            @dataclass(frozen=True)
            class Order:
                id: int
                total: float = 0.0

            def summarize(orders: list[Order], currency="EUR") -> str:
                """Totals the orders, skipping refunds."""
                paid = [o for o in orders if o.total > 0]
                pattern = re.compile(r"\d+\.\d{2}")
                return f"{len(paid)} orders, {sum(o.total for o in paid):.2f} {currency}"
            """"),
        new("java", "Java", "OrderService.java", """
            @Service
            public final class OrderService {
                private static final int MAX_LINES = 50;
                private final OrderRepository repository;

                public OrderService(OrderRepository repository) {
                    this.repository = repository;
                }

                // Orders past the line limit are archived
                public Optional<Order> find(long id) {
                    return repository.findById(id).filter(o -> o.lines().size() <= MAX_LINES);
                }
            }
            """),
        new("go", "Go", "server.go", """
            package main

            import (
                "fmt"
                "net/http"
            )

            type Order struct {
                ID    int     `json:"id"`
                Total float64 `json:"total"`
            }

            func handler(w http.ResponseWriter, r *http.Request) {
                order := Order{ID: 42, Total: 19.99}
                fmt.Fprintf(w, "order %d: %.2f\n", order.ID, order.Total)
            }
            """),
        new("rust", "Rust", "main.rs", """
            use std::collections::HashMap;

            #[derive(Debug, Clone)]
            struct Order<'a> {
                id: u32,
                customer: &'a str,
                total: f64,
            }

            fn main() {
                let mut totals: HashMap<&str, f64> = HashMap::new();
                let order = Order { id: 1, customer: "ada", total: 19.5 };
                *totals.entry(order.customer).or_insert(0.0) += order.total;
                println!("{:?} => {}", order, totals["ada"]);
            }
            """),
        new("c", "C", "checksum.c", """
            #include <stdint.h>
            #include <stdio.h>

            #define BLOCK_SIZE 512

            /* Adds up a buffer, one byte at a time. */
            static uint32_t checksum(const uint8_t *data, size_t length) {
                uint32_t sum = 0;
                for (size_t i = 0; i < length; i++) {
                    sum += data[i];
                }
                return sum;
            }

            int main(void) {
                uint8_t block[BLOCK_SIZE] = {0};
                printf("checksum: %u\n", checksum(block, sizeof block));
                return 0;
            }
            """),
        new("cpp", "C++", "cart.cpp", """
            #include <iostream>
            #include <vector>

            namespace shop {

            template <typename T>
            class Cart {
            public:
                void add(T item) { items_.push_back(std::move(item)); }
                [[nodiscard]] std::size_t size() const noexcept { return items_.size(); }

            private:
                std::vector<T> items_;
            };

            } // namespace shop

            int main() {
                shop::Cart<std::string> cart;
                cart.add("keyboard");
                std::cout << R"(Items: )" << cart.size() << '\n';
            }
            """),
        new("kotlin", "Kotlin", "OrderRepository.kt", """
            @Repository
            class OrderRepository(private val db: Database) {
                suspend fun find(id: Long): Order? = db.query {
                    Orders.select { Orders.id eq id }.singleOrNull()?.toOrder()
                }

                fun describe(order: Order, currency: String): String =
                    "Order ${order.id} in $currency: ${order.total}"

                companion object {
                    const val MAX_LINES = 50
                }
            }
            """),
        new("swift", "Swift", "CartView.swift", """
            import SwiftUI

            struct CartView: View {
                @State private var items: [String] = []
                let currency = "EUR"

                var body: some View {
                    List(items, id: \.self) { item in
                        Text("\(item) in \(currency)")
                    }
                    #if DEBUG
                    .onAppear { print("Cart has \(items.count) items") }
                    #endif
                }
            }
            """),
        new("php", "PHP", "OrderController.php", """
            <?php

            namespace App\Http\Controllers;

            #[Route('/orders')]
            final class OrderController extends Controller
            {
                public function show(int $id): JsonResponse
                {
                    $order = Order::findOrFail($id);
                    // Totals are stored in cents
                    return response()->json(['total' => $order->total / 100, 'label' => "Order $order->id"]);
                }
            }
            """),
        new("ruby", "Ruby", "order.rb", """
            class Order < ApplicationRecord
              has_many :lines, dependent: :destroy
              validates :email, presence: true

              scope :recent, -> { where(created_at: 1.week.ago..) }

              def total
                lines.sum { |line| line.price * line.quantity }
              end

              def summary
                "#{lines.count} lines, total #{total}"
              end
            end
            """),
        new("dart", "Dart", "cart_badge.dart", """
            import 'package:flutter/material.dart';

            class CartBadge extends StatelessWidget {
              const CartBadge({super.key, required this.count});

              final int count;

              @override
              Widget build(BuildContext context) {
                // Hide the badge for an empty cart
                if (count == 0) return const SizedBox.shrink();
                return Text('$count items', style: Theme.of(context).textTheme.labelSmall);
              }
            }
            """),
        new("lua", "Lua", "inventory.lua", """
            -- Tracks stock per item
            local Inventory = {}
            Inventory.__index = Inventory

            function Inventory.new()
              return setmetatable({ items = {} }, Inventory)
            end

            function Inventory:add(name, count)
              self.items[name] = (self.items[name] or 0) + count
            end

            local stock = Inventory.new()
            stock:add("keyboard", 3)
            print(string.format("%d keyboards", stock.items["keyboard"]))
            """),
        new("scala", "Scala", "Orders.scala", """
            import scala.concurrent.Future

            case class Order(id: Long, total: BigDecimal)

            object Orders {
              val MaxLines = 50

              def summary(orders: Seq[Order]): String =
                s"${orders.size} orders, total ${orders.map(_.total).sum}"

              def find(id: Long)(implicit repo: OrderRepository): Future[Option[Order]] =
                repo.byId(id)
            }
            """),
        new("fsharp", "F#", "Orders.fs", """
            module Shop.Orders

            open System

            [<Literal>]
            let MaxLines = 50

            type Order = { Id: int; Total: decimal; PlacedAt: DateTime }

            let summarize (orders: Order list) =
                let total = orders |> List.sumBy (fun o -> o.Total)
                $"{orders.Length} orders, {total:N2} total"

            (* Refunds are negative totals *)
            let refunds orders = orders |> List.filter (fun o -> o.Total < 0m)
            printfn "%s" (summarize [])
            """),
        new("vbnet", "VB.NET", "OrderService.vb", """
            Imports System.Linq

            Public Class OrderService
                Private ReadOnly _orders As New List(Of Order)

                ' Adds an order and returns the new total
                Public Function Add(order As Order) As Decimal
                    _orders.Add(order)
                    Dim total = _orders.Sum(Function(o) o.Total)
                    Console.WriteLine($"{_orders.Count} orders, total {total:N2}")
                    Return total
                End Function
            End Class
            """),
        new("haskell", "Haskell", "Orders.hs", """
            {-# LANGUAGE OverloadedStrings #-}
            module Orders where

            import Data.List (foldl')

            data Order = Order { orderId :: Int, total :: Double }
              deriving (Show, Eq)

            -- | Sums the orders, skipping refunds.
            paidTotal :: [Order] -> Double
            paidTotal = foldl' add 0
              where
                add acc o
                  | total o > 0 = acc + total o
                  | otherwise   = acc
            """),
        new("elixir", "Elixir", "orders.ex", """
            defmodule Shop.Orders do
              @moduledoc "Order queries."
              import Ecto.Query

              def recent(limit \\ 10) do
                from(o in Order, order_by: [desc: o.inserted_at], limit: ^limit)
                |> Repo.all()
              end

              def summary(%Order{id: id, total: total}) do
                "Order #{id}: #{total}"
              end

              defp valid?(order), do: order.total > 0
            end
            """),
        new("clojure", "Clojure", "orders.clj", """
            (ns shop.orders
              (:require [clojure.string :as str]))

            (def max-lines 50)

            (defn paid-total
              "Sums the orders, skipping refunds."
              [orders]
              (->> orders
                   (filter #(pos? (:total %)))
                   (map :total)
                   (reduce + 0)))

            (println (str "Total: " (paid-total [{:id 1 :total 19.5}])))
            """),
        new("graphql", "GraphQL", "orders.graphql", """
            # The recent orders of one customer
            query RecentOrders($customerId: ID!, $first: Int = 10) {
              customer(id: $customerId) {
                name
                orders(first: $first) @include(if: true) {
                  id
                  total
                  placedAt
                }
              }
            }

            type Order {
              id: ID!
              total: Float!
            }
            """),
        new("protobuf", "Protocol Buffers", "orders.proto", """
            syntax = "proto3";

            package shop.orders.v1;

            import "google/protobuf/timestamp.proto";

            // Orders, read and placed.
            service OrderService {
              rpc GetOrder(GetOrderRequest) returns (Order);
            }

            message Order {
              int64 id = 1;
              double total = 2;
              repeated string items = 3;
              google.protobuf.Timestamp placed_at = 4;
            }
            """),
        new("hcl", "HCL (Terraform)", "main.tf", """
            terraform {
              required_providers {
                azurerm = { source = "hashicorp/azurerm", version = "~> 4.0" }
              }
            }

            variable "location" {
              type    = string
              default = "westeurope"
            }

            resource "azurerm_resource_group" "shop" {
              name     = "rg-shop-${var.location}"
              location = var.location
              tags     = { env = lower("Production") }
            }
            """),
        new("bicep", "Bicep", "main.bicep", """
            @description('Where the resources live.')
            param location string = resourceGroup().location

            var storageName = 'shop${uniqueString(resourceGroup().id)}'

            resource storage 'Microsoft.Storage/storageAccounts@2023-05-01' = {
              name: storageName
              location: location
              kind: 'StorageV2'
              sku: {
                name: 'Standard_LRS'
              }
            }

            output endpoint string = storage.properties.primaryEndpoints.blob
            """),
        new("makefile", "Makefile", "Makefile", """
            # Build and test the shop
            CONFIG ?= Release
            OUT := ./publish

            .PHONY: build test publish

            build:
            	dotnet build -c $(CONFIG)

            test: build
            	dotnet test --no-build -c $(CONFIG)

            publish: test
            	dotnet publish src/Shop.Web -c $(CONFIG) -o $(OUT)
            	@echo "Published to $@"
            """),
        new("nginx", "nginx", "shop.conf", """
            upstream shop {
                server 127.0.0.1:5000;
                keepalive 32;
            }

            server {
                listen 443 ssl;
                server_name shop.example.com;
                client_max_body_size 10m;

                location / {
                    proxy_pass http://shop;
                    proxy_set_header Host $host;
                    proxy_read_timeout 60s;
                }
            }
            """),
        new("cmake", "CMake", "CMakeLists.txt", """
            cmake_minimum_required(VERSION 3.25)
            project(Checksum VERSION 1.2 LANGUAGES C)

            set(CMAKE_C_STANDARD 17)
            option(BUILD_TESTS "Build the tests" ON)

            add_executable(checksum src/main.c)
            target_compile_options(checksum PRIVATE -Wall)

            if(BUILD_TESTS)
              enable_testing()
              add_test(NAME smoke COMMAND checksum ${CMAKE_SOURCE_DIR}/data.bin)
            endif()
            """),
        new("batch", "Batch", "build.cmd", """
            @echo off
            setlocal enabledelayedexpansion
            rem Build every project in the src folder
            set CONFIG=Release
            for /d %%p in ("%~dp0src\*") do (
                echo Building %%~np...
                dotnet build "%%p" -c %CONFIG%
                if errorlevel 1 goto :failed
            )
            exit /b 0

            :failed
            echo Build failed with !errorlevel!
            exit /b 1
            """),
        new("r", "R", "sales.R", """
            library(dplyr)

            # Monthly totals, refunds excluded
            sales <- read.csv("sales.csv", stringsAsFactors = FALSE)

            monthly <- sales %>%
              filter(total > 0, !is.na(month)) %>%
              group_by(month) %>%
              summarise(total = sum(total), orders = n())

            if (nrow(monthly) == 0L) stop("No sales found")
            print(head(monthly, 12))
            """),
        new("perl", "Perl", "report.pl", """
            #!/usr/bin/perl
            use strict;
            use warnings;

            my %totals;
            my @orders = ({ id => 1, total => 19.5 }, { id => 2, total => -5 });

            # Refunds are negative totals
            foreach my $order (@orders) {
                next if $order->{total} < 0;
                $totals{paid} += $order->{total};
            }

            my $count = @orders;
            printf "Paid: %.2f of $count orders\n", $totals{paid};
            """),
        new("julia", "Julia", "orders.jl", """
            using Statistics

            struct Order
                id::Int
                total::Float64
            end

            # Mean of the paid orders
            function paid_mean(orders::Vector{Order})
                paid = filter(o -> o.total > 0, orders)
                return isempty(paid) ? 0.0 : mean(o.total for o in paid)
            end

            orders = [Order(1, 19.5), Order(2, -5.0)]
            @show paid_mean(orders)
            println("$orders")
            """),
        new("objectivec", "Objective-C", "CartController.m", """
            #import "CartController.h"

            @interface CartController ()
            @property (nonatomic, strong) NSMutableArray<NSString *> *items;
            @end

            @implementation CartController

            - (void)addItem:(NSString *)name {
                [self.items addObject:name];
                NSLog(@"%lu items in the cart", (unsigned long)self.items.count);
            }

            @end
            """),
        new("latex", "LaTeX", "report.tex", """
            \documentclass{article}
            \usepackage{amsmath}

            \begin{document}
            \section{Sales}
            % Refunds are left out of every total
            Revenue grew by 12\% this quarter, to \$4.2M.
            The mean order is $\bar{x} = \frac{1}{n}\sum_{i=1}^{n} x_i$.
            \[
              T = \sum_{k} p_k q_k
            \]
            \end{document}
            """),
    ];

    /// <summary>A language of the docs' own, built with SyntaxGrammar (see LogGrammar).</summary>
    public static readonly IReadOnlyList<CodeSample> Custom =
    [
        new("log", "App log", "shop.log", """
            2026-10-26T09:14:02.118Z INFO  [Shop.Api] Listening on https://localhost:7043
            2026-10-26T09:14:05.402Z DEBUG [Shop.Orders] Cache miss for order 4211
            2026-10-26T09:14:05.977Z WARN  [Shop.Orders] Slow query: 812 ms (threshold 500 ms)
            2026-10-26T09:14:06.210Z ERROR [Shop.Payments] Charge failed for order 4211: "card_declined"
            System.InvalidOperationException: The payment provider rejected the charge.
               at Shop.Payments.ChargeService.ChargeAsync(Order order) in ChargeService.cs:line 88
            """),
    ];

    /// <summary>Every sample: built in, then Blaizio.Syntax.Languages, then the docs' own.</summary>
    public static readonly IReadOnlyList<CodeSample> All = [.. BuiltIn, .. Extra, .. Custom];
}
