docker login -u kaareseras -p dublin62
cd C:\Users\kaare.SERAS\source\repos\RadmeterDotNetCore
docker build -t readmeter .
docker tag readmeter kaareseras/readmeter
docker push kaareseras/readmeter
