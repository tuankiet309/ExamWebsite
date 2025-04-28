#Docker command Example
- docker run -e "ACCEPT_EULA=Y" -e "SA_PASSWORD=admin@123" -p 1433:1433 --name sql_container -d mcr.microsoft.com/mssql/server:2022-latest
- docer ps "or" docker container ls
- docker run -d --name some-mongo -e MONGO_INITDB_ROOT_USERNAME=mongoadmin -e MONGO_INITDB_ROOT_PASSWORD=secret -p 127.0.0.1:27017:27017 mongo
