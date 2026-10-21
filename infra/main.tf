# Infraestructura de PREPROD (TP8).
#
# Un entorno que no existía: su base en Neon y sus dos servicios en Render. No toca QA ni
# producción —ésos se crearon a mano en el TP6 y siguen así—: preprod es un tercer entorno, que
# nace con `terraform apply` y desaparece con `terraform destroy`.
#
# Las claves no están acá: cada provider las lee del entorno (NEON_API_KEY, RENDER_API_KEY y
# RENDER_OWNER_ID). El estado tampoco: vive en terraform.tfstate, en la máquina del que aplica, y
# el .gitignore lo deja afuera del repositorio porque guarda contraseñas en texto plano.

terraform {
  required_version = ">= 1.5"

  # Versiones fijas: sin esto, un `init` de otro día puede bajar otra cosa y lo que ayer
  # compilaba deja de compilar. El .terraform.lock.hcl fija además el hash de cada una.
  required_providers {
    neon = {
      source  = "kislerdm/neon"
      version = "0.18.0"
    }
    render = {
      source  = "render-oss/render"
      version = "1.9.1"
    }
    random = {
      source  = "hashicorp/random"
      version = "3.9.1"
    }
  }
}

provider "neon" {}   # lee NEON_API_KEY
provider "render" {} # lee RENDER_API_KEY y RENDER_OWNER_ID

# ---------------------------------------------------------------------------
# La base de preprod
# ---------------------------------------------------------------------------
# Un proyecto de Neon propio, y no una base más en el proyecto de QA y producción: así preprod se
# puede destruir entero sin rozar nada de los otros dos.

resource "neon_project" "preprod" {
  name   = "joyeria-preprod"
  org_id = var.neon_org_id # la cuenta de Neon es una organización: la API no crea proyectos sin esto

  # Lo más parecido posible a producción: la misma región y la misma versión de Postgres que el
  # proyecto de QA y producción.
  region_id  = var.neon_region
  pg_version = var.pg_version

  # El provider pide por defecto un día de historial y el plan gratuito permite seis horas. Sin
  # esta línea el primer apply falla, con un error que no dice qué hacer.
  history_retention_seconds = 21600
}

resource "neon_role" "preprod" {
  project_id = neon_project.preprod.id
  branch_id  = neon_project.preprod.default_branch_id
  name       = "app_preprod_owner"
}

resource "neon_database" "preprod" {
  project_id = neon_project.preprod.id
  branch_id  = neon_project.preprod.default_branch_id
  name       = "app_preprod"
  owner_name = neon_role.preprod.name
}

# ---------------------------------------------------------------------------
# Los dos servicios de preprod
# ---------------------------------------------------------------------------
# Se CREAN y se DESTRUYEN, nunca se modifican: el provider de Render no puede actualizar un
# servicio del plan gratuito. Si algo tiene que cambiar, se recrea (destroy + apply, o
# `terraform apply -replace=<recurso>`). Terraform no lo deduce solo: planea un update, el update
# falla, y el apply se corta.

# La clave con la que la api firma los tokens. La api no arranca sin ella, y no la escribe nadie:
# la genera Terraform la primera vez y vive sólo en el estado. Es distinta de la de QA y de la de
# producción, así que un token de un entorno no sirve en otro.
resource "random_password" "jwt" {
  length  = 48
  special = false
}

locals {
  # La cadena de conexión se ARMA con lo que devolvió Neon: nadie la copia ni la pega. Ése fue el
  # error más caro del TP6, un entorno apuntando a la base de otro. Va en el formato clave=valor
  # que pide Npgsql, no en la URI que muestra Neon.
  conexion = "Host=${neon_project.preprod.database_host};Database=${neon_database.preprod.name};Username=${neon_role.preprod.name};Password=${neon_role.preprod.password};SSL Mode=Require"
}

resource "render_web_service" "api" {
  name   = "joyeria-api-preprod"
  plan   = "free"
  region = var.region

  runtime_source = {
    image = {
      image_url = var.imagen_api # SIN la etiqueta: va en su propio campo
      tag       = var.etiqueta
    }
  }

  env_vars = {
    ConnectionStrings__Default = { value = local.conexion }
    Jwt__Key                   = { value = random_password.jwt.result }

    # La base nace vacía. Con estos dos, la api crea sus tablas y carga los datos iniciales al
    # arrancar, como en QA y en producción: Terraform crea la base, no el esquema.
    AplicarMigracionesAlArrancar = { value = "true" }
    SembrarDatosIniciales        = { value = "true" }
  }
}

resource "render_web_service" "front" {
  name   = "joyeria-front-preprod"
  plan   = "free"
  region = var.region

  runtime_source = {
    image = {
      image_url = var.imagen_front
      tag       = var.etiqueta # la MISMA imagen del front que corre en QA y en producción
    }
  }

  env_vars = {
    # La dirección de la api sale de la api, no de un copy-paste. Sin barra final: con una, nginx
    # mandaría todos los pedidos a "/" y cada llamada daría 404.
    BACKEND_URL  = { value = trimsuffix(render_web_service.api.url, "/") }
    DNS_RESOLVER = { value = "8.8.8.8" }
    PORT         = { value = "80" } # el puerto en el que escucha nginx adentro de la imagen
  }
}
