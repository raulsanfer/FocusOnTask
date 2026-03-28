# FocusOnTask

FocusOnTask es una aplicacion de escritorio construida con .NET MAUI y Blazor Hybrid orientada a gestionar el trabajo diario con una estructura simple: proyectos, tareas y sesiones de trabajo.

## Finalidad del proyecto

El objetivo de la aplicacion es ayudar a concentrar el trabajo en una unica vista operativa, permitiendo:

- Organizar tareas dentro de proyectos.
- Iniciar y cerrar jornadas de trabajo.
- Asociar una tarea activa a una sesion en curso.
- Registrar el tiempo real trabajado por tarea.
- Comparar horas estimadas frente a horas invertidas.
- Disponer de una base preparada para futuros reportes de productividad.

En lugar de ser un gestor de proyectos complejo, FocusOnTask busca servir como una herramienta ligera para seguimiento personal o de equipos pequenos, poniendo el foco en la ejecucion diaria.

## Resumen funcional

Actualmente el proyecto incluye:

- Dashboard principal con metricas globales, sesion activa, tareas destacadas y actividad reciente.
- Gestion de proyectos con fecha de inicio, fecha fin opcional y conteo de tareas abiertas o completadas.
- Gestion de tareas con estimacion en horas, estado y asociacion opcional a proyecto.
- Control de sesiones de trabajo con duracion por defecto configurable.
- Registro de tiempo efectivo trabajado mediante logs por segmento de tarea.
- Persistencia local en SQLite con creacion automatica de la base de datos al iniciar la app.

## Modelo de trabajo

La aplicacion se apoya en estas entidades principales:

- `Project`: agrupa tareas y define una ventana temporal de trabajo.
- `TaskItem`: representa una tarea con estimacion, tiempo trabajado y estado.
- `WorkSession`: representa una jornada o bloque principal de trabajo.
- `TaskSessionLog`: guarda los segmentos de tiempo realmente dedicados a cada tarea.
- `AppConfiguration`: almacena la configuracion general, como la duracion por defecto de la jornada.

## Tecnologias utilizadas

- `.NET 9`
- `.NET MAUI`
- `Blazor Hybrid`
- `Entity Framework Core`
- `SQLite`

## Estado actual

El proyecto esta orientado en este momento a Windows y compila sobre `net9.0-windows10.0.19041.0`.

La seccion de reportes ya esta prevista en la interfaz, aunque todavia se encuentra en una fase inicial. La base de datos ya conserva la informacion necesaria para construir estadisticas y analitica en iteraciones futuras.

## Ejecucion local

### Requisitos

- SDK de `.NET 9`
- Workload de `.NET MAUI`
- Entorno Windows compatible con `windows10.0.19041.0`

### Arranque

```bash
dotnet build
dotnet run
```

La aplicacion crea automaticamente su base de datos SQLite local en el directorio de datos de la aplicacion, usando el fichero `focusontask.db`.

## Estructura general

- `Components/Pages`: vistas principales de la aplicacion.
- `Models`: entidades de dominio.
- `Services`: logica de negocio y coordinacion de la operativa.
- `Data`: acceso a datos y configuracion de Entity Framework Core.
- `Resources`: iconos, fuentes e imagenes del proyecto.

## Vision del producto

FocusOnTask pretende convertirse en una herramienta sencilla para planificar, ejecutar y revisar trabajo real sin sobrecarga innecesaria. Su valor esta en unir planificacion basica con trazabilidad del tiempo dedicado, manteniendo una experiencia directa y centrada en el foco diario.
